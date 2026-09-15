<?php

namespace App\Http\Controllers;

use App\Models\Ayarlar;
use App\Models\User;
use App\Models\BayiModel;
use App\Models\MusteriModel;
use App\Models\LisansModel;
use App\Models\LisansPaketModel;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Carbon\Carbon;

class AyarlarController extends Controller
{
    public function Comingsoon()
    {
        return view('Comingsoon');
    }

    public function MicroService()
    {
        return view('MicroService');
    }

    public function PayTR()
    {
        $paytr = Ayarlar::first();
        return view('PayTR', compact('paytr'));
    }

    public function SMS()
    {
        $sms = Ayarlar::first();
        return view('SMS', compact('sms'));
    }

    public function UpdatePayTR(Request $request)
    {

        $ayarlar = Ayarlar::first();
        $ayarlar ->alan6 = $request->input('alan6');
        $ayarlar ->alan7 = $request->input('alan7');
        $ayarlar ->alan8 = $request->input('alan8');

        
        $ayarlar->update();

        return redirect()->back()->with('success', 'PayTR Bilgileri Başarılı Şekilde Güncellendi.')->with('run_success_js', true);
    }
    public function UpdateSMS(Request $request)
    {

        $ayarlar = Ayarlar::first();
        $ayarlar ->alan1 = $request->input('alan1');
        $ayarlar ->alan2 = $request->input('alan2');
        $ayarlar ->alan3 = $request->input('alan3');
        $ayarlar ->alan4 = $request->input('alan4');
        $ayarlar ->alan5 = $request->input('alan5');

        
        $ayarlar->update();

        return redirect()->back()->with('success', 'Sms Bilgileri Başarılı Şekilde Güncellendi.')->with('run_success_js', true);
    } 
 
    public function Certificate()
    {
        $user = Auth::user();

        $bayi = BayiModel::where('UserId', $user->id)->first();

        if ($user->role == 'admin') {
            $Musteriler = MusteriModel::with('kimbubayi')->get();
        } else {
            $kimlikler = [$user->id];

            if ($user->is_main_dealer) {
                $subDealerIds = User::where('parent_id', $user->id)->pluck('id')->toArray();
                $kimlikler = array_merge($kimlikler, $subDealerIds);
            }

            $Musteriler = MusteriModel::with('kimbubayi')->whereIn('Bayi', $kimlikler)->get();
        }

        $bayiAdlari = User::whereIn('id', $Musteriler->pluck('Bayi')->filter()->unique())
            ->pluck('name', 'id');

        $anaPaketAdlari = LisansPaketModel::query()
            ->whereRaw('LOWER(PaketTipi) = ?', ['paket'])
            ->pluck('PaketName')
            ->map(fn ($n) => strtolower((string) $n))
            ->all();

        $lisanslar = LisansModel::whereIn('Musteri', $Musteriler->pluck('id'))
            ->where('Tipi', 'satis')
            ->get()
            ->groupBy('Musteri');

        $lisansTarihleri = [];
        foreach ($Musteriler as $musteri) {
            $lisansTarihleri[$musteri->id] = $this->musteriLisansBaslangic(
                $lisanslar->get($musteri->id) ?? collect(),
                $anaPaketAdlari
            );
        }

        return view('sertifika.index', compact('Musteriler', 'bayi', 'bayiAdlari', 'lisansTarihleri'));
    }

    private function musteriLisansBaslangic($lisanslar, array $anaPaketAdlari): string
    {
        $anaStarts = [];
        $allStarts = [];

        foreach ($lisanslar as $lisans) {
            $start = $this->lisansKayitBaslangic($lisans);
            if ($start) {
                $allStarts[] = $start;
            }
            foreach ($this->flattenLicensePackages($lisans) as $paket) {
                if ((int) ($paket['status'] ?? 0) !== 1) {
                    continue;
                }
                $name = strtolower((string) ($paket['paketName'] ?? ''));
                if ($name !== '' && in_array($name, $anaPaketAdlari, true) && $start) {
                    $anaStarts[] = $start;
                }
            }
        }

        $pick = $anaStarts ?: $allStarts;
        if (!$pick) {
            return '';
        }
        usort($pick, fn ($a, $b) => $a <=> $b);

        return $pick[0]->format('Y/m/d');
    }

    private function lisansKayitBaslangic($lisans): ?Carbon
    {
        if ($lisans->created_at) {
            return $lisans->created_at->copy()->startOfDay();
        }

        return null;
    }

    private function flattenLicensePackages($lisans): array
    {
        $lisansData = json_decode($lisans->Lisans, true);
        $lisansData = is_array($lisansData) ? $lisansData : [];
        $ham = [];
        foreach ($lisansData as $item) {
            if (!is_array($item)) {
                continue;
            }
            if (isset($item['yazarkasa']) && is_array($item['yazarkasa'])) {
                foreach ($item['yazarkasa'] as $yk) {
                    if (isset($yk['paketName'])) {
                        $ham[] = $yk;
                    }
                }
            }
            if (isset($item['paketName'])) {
                $ham[] = $item;
            } elseif (is_array($item)) {
                foreach ($item as $sub) {
                    if (is_array($sub) && isset($sub['paketName'])) {
                        $ham[] = $sub;
                    }
                }
            }
        }

        return $ham;
    }
}
