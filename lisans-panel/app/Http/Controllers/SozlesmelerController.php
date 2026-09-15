<?php

namespace App\Http\Controllers;

use App\Models\LisansModel;
use App\Models\User;
use App\Models\BayiModel;
use App\Models\MusteriModel;
use Illuminate\Http\Request;
use App\Models\SozlesmeModel;
use Barryvdh\DomPDF\Facade\Pdf;
use App\Models\LisansPaketModel;
use Illuminate\Support\Facades\Auth;

class SozlesmelerController extends Controller
{
    public const TIP_SLUGS = [
        'satis' => 3,
        'bakim' => 1,
        'servis' => 4,
    ];

    public const TIP_BASLIK = [
        'satis' => 'Satış sözleşmesi',
        'bakim' => 'Bakım sözleşmesi',
        'servis' => 'Servis sözleşmesi',
    ];

    public static function tipSlug(?int $tipi): ?string
    {
        return match ($tipi) {
            1 => 'bakim',
            3 => 'satis',
            4 => 'servis',
            default => null,
        };
    }

    public function Agreement(Request $request)
    {
        $tipSlug = $request->query('tip');
        $aktifTip = self::TIP_SLUGS[$tipSlug] ?? null;
        $sayfaBaslik = self::TIP_BASLIK[$tipSlug] ?? 'Sözleşme İşlemleri';

        $query = SozlesmeModel::query();
        if ($aktifTip !== null) {
            $query->where('tipi', $aktifTip);
        }

        $Sozlesmeler = $query->get();
        $Musteriler = MusteriModel::all();
        $Paket = LisansPaketModel::all();
        return view('sozlesmeler.index', compact('Sozlesmeler', 'Musteriler', 'Paket', 'aktifTip', 'sayfaBaslik', 'tipSlug'));
    }

    public function AddAgreement(Request $request)
    {
        try {
            $Kimlik = Auth::user()->id;
            $Sozlesme = new SozlesmeModel();
            $Sozlesme->tipi = $request->input('tipi');
            $izinliTipler = array_values(self::TIP_SLUGS);
            if (! in_array((int) $Sozlesme->tipi, $izinliTipler, true)) {
                return redirect()->route('Agreement')->with('warning', 'Formda eksik veya hatalı veriler var')->with('run_warning_js', true);
            }
            $Sozlesme->bayi = $Kimlik;
            $Sozlesme->musteri = $request->input('musteri');
            $Sozlesme->destekbedeli = $request->input('destekbedeli');
            $Sozlesme->kullanicisayisi = $request->input('kullanicisayisi');
            $Sozlesme->surum = $request->input('surum');
            $Sozlesme->odemesekli = $request->input('odemesekli');
            $Sozlesme->paket = $request->input('paket');
            $Sozlesme->bitistarihi = $request->input('bitistarihi');
            $Sozlesme->imzalanmisevrak = 0;

            $Sozlesme->save();
            $SozlesmeId = $Sozlesme->id;

            activity('Sözleşme İşlemleri')
            ->causedBy(null) 
            ->tap(function ($activity) {
                $activity->causer_type = 'Add'; 
                $activity->causer_id =  Auth::user()->id; 
            })
            ->withProperties(['id' => $Sozlesme->id])
            ->log('Sözleşme Oluşturuldu');

            $geri = $this->agreementRedirect((int) $Sozlesme->tipi);
            if ($SozlesmeId) {
                return redirect()->to($geri)->with('success', 'Sözleşme Başarılı Şekilde Oluşturuldu')->with('run_success_js', true);
            } else {
                return redirect()->to($geri)->with('error', 'Sözleşme Oluşturulamadı.')->with('run_error_js', true);
            }
        } catch (\Illuminate\Validation\ValidationException $e) {
          
            return redirect()->to($this->agreementRedirect((int) $request->input('tipi')))->with('warning', 'Formda eksik veya hatalı veriler var')->with('run_warning_js', true);
        }
    }

    private function agreementRedirect(?int $tipi): string
    {
        $slug = self::tipSlug($tipi);
        return $slug ? route('Agreement', ['tip' => $slug]) : route('Agreement');
    }

    public function LicenseAgreement(Request $request, $siparisNo, $tip)
    {
        try {
        if (! isset(self::TIP_SLUGS[$tip])) {
            abort(404);
        }

        $lisans = LisansModel::where('SiparisNo', $siparisNo)->firstOrFail();
        $musteri = MusteriModel::find($lisans->Musteri);
        if (! $musteri) {
            abort(404);
        }

        $user = Auth::user();
        if (($user->role ?? '') !== 'admin') {
            $ok = ((int) $lisans->Bayi === (int) $user->id) || ((int) $musteri->Bayi === (int) $user->id);
            if (! $ok) {
                abort(403);
            }
        }

        $paketFiltre = (string) $request->query('paket', '');
        $paketModelleri = LisansPaketModel::all()->keyBy('PaketName');
        $lisansData = json_decode($lisans->Lisans, true);
        $lisansData = is_array($lisansData) ? $lisansData : [];
        $ham = [];
        foreach ($lisansData as $item) {
            if (isset($item['yazarkasa']) && is_array($item['yazarkasa'])) {
                foreach ($item['yazarkasa'] as $yk) {
                    if (isset($yk['paketName'])) {
                        $ham[] = $yk;
                    }
                }
            } elseif (isset($item['paketName'])) {
                $ham[] = $item;
            } elseif (is_array($item)) {
                foreach ($item as $sub) {
                    if (is_array($sub) && isset($sub['paketName'])) {
                        $ham[] = $sub;
                    }
                }
            }
        }

        $aktifPaketler = [];
        $toplamTutar = 0.0;
        foreach ($ham as $p) {
            if ((int) ($p['status'] ?? 0) !== 1) {
                continue;
            }
            if ($paketFiltre !== '' && ($p['paketName'] ?? '') !== $paketFiltre) {
                continue;
            }
            $model = $paketModelleri->get($p['paketName']);
            $fiyat = $model ? (float) $model->PaketFiyati : 0.0;
            $toplamTutar += $fiyat;
            $aktifPaketler[] = [
                'adi' => $model->PaketAdi ?? $p['paketName'],
                'sure' => $p['date'] ?? '—',
                'tutar' => $fiyat,
            ];
        }

        $bayiAdi = optional($musteri->kimbubayi)->Unvan
            ?: optional($lisans->bayilers)->name
            ?: '—';
        $sayfaBaslik = self::TIP_BASLIK[$tip];
        $tarih = now()->format('d.m.Y');
        $sozlesmeNo = $lisans->SiparisNo.'-'.strtoupper($tip);

        return view('sozlesmeler.lisans', compact(
            'lisans',
            'musteri',
            'tip',
            'sayfaBaslik',
            'aktifPaketler',
            'toplamTutar',
            'bayiAdi',
            'tarih',
            'sozlesmeNo'
        ));
        } catch (\Symfony\Component\HttpKernel\Exception\HttpException $e) {
            throw $e;
        } catch (\Throwable $e) {
            abort(404);
        }
    }

    public function DownloadAgreement($id)
{
    $sozlesme = SozlesmeModel::where('id', $id)->first();
    $lisans = LisansModel::where('Musteri', $sozlesme->musteri)->first();
    
    
    $bayi = BayiModel::where('UserId', $sozlesme->bayi)->first();
    
    $pdf = PDF::loadView('sozlesmeler.Sozlesmelerpdf', compact('sozlesme', 'bayi', 'lisans'));
    return $pdf->download('teklif_'.$sozlesme->id.'.pdf');
}
}
