<?php

namespace App\Http\Controllers;

use App\Models\LisansModel;
use App\Models\User;
use Illuminate\Support\Str;
use App\Models\MusteriModel;
use App\Models\BayiOzelFiyatModel;
use Illuminate\Http\Request;
use App\Models\SozlesmeModel;
use App\Models\LisansPaketModel;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Log;
use App\Models\LicenseProduct;
use App\Models\LicenseFis;
use App\Models\LicenseFatura;
use App\Models\LicenseCari;
use App\Models\LicenseMasa;
use App\Models\LicensePatron;
use App\Models\LicenseYedek;

class MusteriController extends Controller
{
    private function resolveMusteriId($id)
    {
        $raw = urldecode((string) $id);
        if ($raw !== '' && ctype_digit($raw)) {
            return (int) $raw;
        }
        try {
            return decrypt($raw);
        } catch (\Throwable $e) {
            abort(404);
        }
    }

    public function Customer()
    {
        // Eager Loading kullanarak performans artışı sağlıyoruz (N+1 problemini önler)
        $Bayiler = MusteriModel::with('kimbubayi')->get();

        $Bayi = User::where('role', 'agent')->get();
        $Sozlesme = SozlesmeModel::where('tipi', 1)->get();

        return view('musteri.index', compact('Bayiler', 'Sozlesme', 'Bayi'));
    }

    public function agentCustomer()
    {
        $user = Auth::user();

        // İşlemi yapan kullanıcının ID'si (Kendisi)
        $kimlikler = [$user->id];

        // Eğer kullanıcı bir ANA BAYİ ise, alt bayilerinin ID'lerini de listeye ekle
        if ($user->is_main_dealer) {
            $subDealerIds = User::where('parent_id', $user->id)->pluck('id')->toArray();
            $kimlikler = array_merge($kimlikler, $subDealerIds);
        }

        // "Bayi" sütunu bizim topladığımız ID listesinde olan tüm müşterileri getir
        $Bayiler = MusteriModel::whereIn('Bayi', $kimlikler)->get();

        $Sozlesme = SozlesmeModel::where('tipi', 1)->get();

        return view('musteri.index', compact('Bayiler', 'Sozlesme'));
    }

    // Admin Müşteri Ekleme
    public function AddCustomer(Request $request)
    {
        // Validasyon Kuralları
        $rules = [
            'Unvan' => 'required',
            'TabelaAdi' => 'required',
            'VergiNo' => 'required|unique:musteri,VergiNo',
            'VergiDairesi' => 'required',
            'Yetkili' => 'required',
            'YetkiliGsm' => 'required',
            'bayi' => 'required',
            'Email' => 'required|email',
            'Il' => 'required',
            'Ilce' => 'required',
            'Adres' => 'required',
            'Ulke' => 'required',
            'Telefon' => 'nullable'
        ];

        // Türkçe Mesajlar
        $messages = [
            'Unvan.required' => 'Firma ünvanı zorunludur.',
            'TabelaAdi.required' => 'Tabela adı zorunludur.',
            'VergiNo.required' => 'Vergi numarası zorunludur.',
            'VergiNo.unique' => 'Bu vergi numarası zaten kayıtlı.',
            'VergiDairesi.required' => 'Vergi dairesi zorunludur.',
            'Yetkili.required' => 'Yetkili adı soyadı zorunludur.',
            'YetkiliGsm.required' => 'GSM numarası zorunludur.',
            'bayi.required' => 'Lütfen bir bayi seçiniz.',
            'Email.required' => 'E-Posta adresi zorunludur.',
            'Il.required' => 'Lütfen İl seçiniz.',
            'Ilce.required' => 'Lütfen İlçe seçiniz.',
            'Adres.required' => 'Adres girmek zorunludur.',
            'Ulke.required' => 'Ülke seçimi zorunludur.'
        ];

        $validator = \Illuminate\Support\Facades\Validator::make($request->all(), $rules, $messages);

        if ($validator->fails()) {
            return response()->json([
                'status' => 'error',
                'errors' => $validator->errors()
            ]);
        }

        try {
            $Dealer = new MusteriModel();
            $Dealer->Bayi = $request->input('bayi');
            $Dealer->Unvan = $request->input('Unvan');
            $Dealer->TabelaAdi = $request->input('TabelaAdi');
            $Dealer->VergiNo = $request->input('VergiNo');
            $Dealer->VergiDairesi = $request->input('VergiDairesi');
            $Dealer->Yetkili = $request->input('Yetkili');
            $Dealer->YetkiliGsm = $request->input('YetkiliGsm');
            $Dealer->Telefon = $request->input('Telefon');
            $Dealer->Il = $request->input('Il');
            $Dealer->Ilce = $request->input('Ilce');
            $Dealer->Adres = $request->input('Adres');
            $Dealer->Ulke = $request->input('Ulke');
            $Dealer->Email = $request->input('Email');
            $Dealer->save();

            $DealerId = $Dealer->id;

            activity('Müşteri İşlemleri')
                ->causedBy(Auth::user())
                ->withProperties(['id' => $Dealer->id])
                ->log('Müşteri Oluşturuldu (Admin)');

            $redirectUrl = route('License', ['id' => urlencode(encrypt($DealerId))]);

            return response()->json([
                'status' => 'success',
                'message' => 'Müşteri Başarılı Şekilde Oluşturuldu',
                'redirect_url' => $redirectUrl
            ]);

        } catch (\Exception $e) {
            return response()->json([
                'status' => 'error',
                'message' => 'Sistem hatası: ' . $e->getMessage()
            ]);
        }
    }

    // Agent (Bayi) Müşteri Ekleme
    public function agentAddCustomer(Request $request)
    {
        // Validasyon Kuralları
        $rules = [
            'Unvan' => 'required',
            'TabelaAdi' => 'required',
            'VergiNo' => 'required|unique:musteri,VergiNo',
            'VergiDairesi' => 'required',
            'Yetkili' => 'required',
            'YetkiliGsm' => 'required',
            'Email' => 'required|email',
            'Il' => 'required',
            'Ilce' => 'required',
            'Adres' => 'required',
            'Ulke' => 'required',
        ];

        $messages = [
            'Unvan.required' => 'Firma ünvanı zorunludur.',
            'TabelaAdi.required' => 'Tabela adı zorunludur.',
            'VergiNo.required' => 'Vergi numarası zorunludur.',
            'VergiNo.unique' => 'Bu vergi numarası zaten kayıtlı.',
            'VergiDairesi.required' => 'Vergi dairesi zorunludur.',
            'Yetkili.required' => 'Yetkili adı soyadı zorunludur.',
            'YetkiliGsm.required' => 'GSM numarası zorunludur.',
            'Email.required' => 'E-Posta adresi zorunludur.',
            'Il.required' => 'Lütfen İl seçiniz.',
            'Ilce.required' => 'Lütfen İlçe seçiniz.',
            'Adres.required' => 'Adres girmek zorunludur.',
            'Ulke.required' => 'Ülke seçimi zorunludur.'
        ];

        $validator = \Illuminate\Support\Facades\Validator::make($request->all(), $rules, $messages);

        if ($validator->fails()) {
            return response()->json([
                'status' => 'error',
                'errors' => $validator->errors()
            ]);
        }

        try {
            $Dealer = new MusteriModel();
            $Dealer->Bayi = Auth::user()->id;
            $Dealer->Unvan = $request->input('Unvan');
            $Dealer->TabelaAdi = $request->input('TabelaAdi');
            $Dealer->VergiNo = $request->input('VergiNo');
            $Dealer->VergiDairesi = $request->input('VergiDairesi');
            $Dealer->Yetkili = $request->input('Yetkili');
            $Dealer->YetkiliGsm = $request->input('YetkiliGsm');
            $Dealer->Telefon = $request->input('Telefon');
            $Dealer->Il = $request->input('Il');
            $Dealer->Ilce = $request->input('Ilce');
            $Dealer->Adres = $request->input('Adres');
            $Dealer->Ulke = $request->input('Ulke');
            $Dealer->Email = $request->input('Email');
            $Dealer->save();

            $DealerId = $Dealer->id;

            activity('Müşteri İşlemleri')
                ->causedBy(Auth::user())
                ->withProperties(['id' => $Dealer->id])
                ->log('Müşteri Oluşturuldu (Agent)');

            $redirectUrl = route('agentLicense', ['id' => urlencode(encrypt($DealerId))]);

            return response()->json([
                'status' => 'success',
                'message' => 'Müşteri Başarılı Şekilde Oluşturuldu',
                'redirect_url' => $redirectUrl
            ]);

        } catch (\Exception $e) {
            return response()->json([
                'status' => 'error',
                'message' => 'Sistem hatası: ' . $e->getMessage()
            ]);
        }
    }

    public function UpdateCustomer(Request $request, $id)
    {
        try {
            $Dealer = MusteriModel::find($id);
            $Dealer->Unvan = $request->input('Unvan');
            if ($request->filled('Bayi')) {
                $Dealer->Bayi = $request->input('Bayi');
            }
            $Dealer->TabelaAdi = $request->input('TabelaAdi');
            $Dealer->VergiNo = $request->input('VergiNo');
            $Dealer->VergiDairesi = $request->input('VergiDairesi');
            $Dealer->Yetkili = $request->input('Yetkili');
            $Dealer->YetkiliGsm = $request->input('YetkiliGsm');
            $Dealer->Telefon = $request->input('Telefon');
            $Dealer->Il = $request->input('Il');
            if ($request->has('Ilce')) {
                $Dealer->Ilce = $request->input('Ilce');
            }
            $Dealer->Adres = $request->input('Adres');
            $Dealer->Ulke = $request->input('Ulke');
            $Dealer->Email = $request->input('Email');
            $Dealer->save();
            $DealerId = $Dealer->id;

            activity('Müşteri İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Update';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['id' => $Dealer->id])
                ->log('Müşteri Güncellendi');

            if ($DealerId) {
                return redirect()->route('EditCustomer', ['id' => urlencode(encrypt($DealerId))])->with('success', 'Müşteri Başarılı Şekilde Güncellendi')->with('run_success_js', true);
            } else {
                return redirect()->route('Customer')->with('error', 'Müşteri Düzenlenemedi.')->with('run_error_js', true);
            }
        } catch (\Illuminate\Validation\ValidationException $e) {
            return redirect()->route('Customer')->with('warning', 'Formda eksik veya hatalı veriler var')->with('run_warning_js', true);
        }
    }

    public function DeleteCustomer(Request $request, $id)
    {
        try {
            $id = decrypt(urldecode($id));

            $Dealer = MusteriModel::find($id);

            if (!$Dealer) {
                return redirect()->route('Customer')->with('error', 'Bayi bulunamadı')->with('run_error_js', true);
            }

            $LisansCount = LisansModel::where('Musteri', $id)->count();

            $Dealer->delete();

            if ($LisansCount > 0) {
                LisansModel::where('Musteri', $id)->delete();
                $logMessage = 'Müşteri ve lisansları silindi';
            } else {
                $logMessage = 'Müşteri silindi (lisansı yoktu)';
            }

            activity('Müşteri İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Delete';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['id' => $id])
                ->log($logMessage);

            return redirect()->route('Customer')->with('success', $logMessage)->with('run_success_js', true);
        } catch (\Exception $e) {
            return redirect()->route('Customer')->with('error', 'Silme işlemi sırasında bir hata oluştu: ' . $e->getMessage())->with('run_error_js', true);
        }
    }

    public function agentDeleteCustomer(Request $request, $id)
    {
        try {
            $id = decrypt(urldecode($id));

            $Dealer = MusteriModel::find($id);
            $Lisans = LisansModel::where('Musteri', $id)->get();

            if (!$Dealer) {
                return redirect()->route('Customer')->with('error', 'Bayi bulunamadı')->with('run_error_js', true);
            }

            $Dealer->delete();
            $Lisans->delete();

            activity('Müşteri İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Delete';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['id' => $id])
                ->log('Müşteri Silindi');

            return redirect()->route('agentCustomer')->with('success', 'Müşteri Başarılı Şekilde Silindi')->with('run_success_js', true);
        } catch (\Exception $e) {
            return redirect()->route('agentCustomer')->with('error', 'Silme işlemi sırasında bir hata oluştu: ' . $e->getMessage())->with('run_error_js', true);
        }
    }

public function EditCustomer(Request $request, $id)
{
    $id = $this->resolveMusteriId($id);
    $Musteri = MusteriModel::find($id);
    if (!$Musteri) {
        abort(404);
    }
    $Bayi = User::where('role', 'agent')->get();
    $Lisanslar = LisansModel::where('Musteri', $id)->get();
    $saylisans = LisansModel::where('Musteri', $id)->where('Tipi', 'satis')->count();
    $sayteklif = LisansModel::where('Musteri', $id)->where('Tipi', 'teklif')->count();

    $LisansPaket = LisansPaketModel::all();

    $jsonLisanslar = [];

    foreach ($Lisanslar as $Lisans) {
        try {
            $programlar = json_decode($Lisans->Lisans, true);
            if (isset($programlar['yazarkasa']) && is_array($programlar['yazarkasa'])) {
                foreach ($programlar['yazarkasa'] as &$paket) {
                    if (isset($paket['date']) && $paket['date'] != 'N/A') {
                        try {
                            $date = new \DateTime($paket['date']);
                            $date->modify('+1 year');
                            $paket['date'] = $date->format('d.m.Y');
                        } catch (\Throwable $e) {}
                    }
                }
                unset($paket);
            }
            $jsonLisanslar[] = $programlar;
        } catch (\Throwable $e) {
            $jsonLisanslar[] = [];
        }
    }

    // ═══════════════════════════════════════════════════════════
    // POS SYNC VERİLERİ
    // ═══════════════════════════════════════════════════════════
    $musteriLisansAnahtarlari = $Lisanslar->pluck('Anahtar')->filter()->toArray();

    $syncProducts   = collect();
    $syncFisler     = collect();
    $syncFaturalar  = collect();
    $syncCariler    = collect();
    $syncMasalar    = collect();
    $syncToplamCiro = 0;
    $patronlar      = collect();
	$syncYedekler   = collect();

    if (!empty($musteriLisansAnahtarlari)) {
        $syncProducts   = LicenseProduct::whereIn('license_key', $musteriLisansAnahtarlari)->latest()->limit(100)->get();
        $syncFisler     = LicenseFis::whereIn('license_key', $musteriLisansAnahtarlari)->latest('tarih')->limit(80)->get();
        $syncFaturalar  = LicenseFatura::whereIn('license_key', $musteriLisansAnahtarlari)->latest('tarih')->limit(80)->get();
        $syncCariler    = LicenseCari::whereIn('license_key', $musteriLisansAnahtarlari)->latest()->limit(80)->get();
        $syncMasalar    = LicenseMasa::whereIn('license_key', $musteriLisansAnahtarlari)->latest()->limit(80)->get();
        $syncToplamCiro = LicenseFis::whereIn('license_key', $musteriLisansAnahtarlari)->sum('toplam');
        $patronlar      = LicensePatron::whereIn('lisans_anahtar', $musteriLisansAnahtarlari)
                            ->orderBy('created_at', 'desc')->limit(50)->get();
		$syncYedekler   = LicenseYedek::whereIn('license_key', $musteriLisansAnahtarlari)
									->orderByDesc('yedek_tarihi')->limit(50)->get();
    }

    return view('musteri.edit', compact(
        'Musteri',
        'Lisanslar',
        'saylisans',
        'sayteklif',
        'LisansPaket',
        'jsonLisanslar',
        'Bayi',
        'syncProducts',
        'syncFisler',
        'syncFaturalar',
        'syncCariler',
        'syncMasalar',
        'syncToplamCiro',
        'patronlar',
        'musteriLisansAnahtarlari',
        'syncYedekler',
    ));
}

    public function agentEditCustomer(Request $request, $id)
    {
        $id = $this->resolveMusteriId($id);
        $Musteri = MusteriModel::find($id);
        if (!$Musteri) {
            abort(404);
        }
        $Lisanslar = LisansModel::where('Musteri', $id)->get();
        $saylisans = LisansModel::where('Musteri', $id)->where('Tipi', 'satis')->count();
        $sayteklif = LisansModel::where('Musteri', $id)->where('Tipi', 'teklif')->count();

        // DÜZELTME: $lisansPaketler -> $LisansPaket olarak değişken adı düzeltildi
        $LisansPaket = LisansPaketModel::all();

        $jsonLisanslar = [];

        foreach ($Lisanslar as $Lisans) {
            $programlar = json_decode($Lisans->Lisans, true);

            if (isset($programlar['yazarkasa'])) {
                foreach ($programlar['yazarkasa'] as &$paket) {
                    if (isset($paket['date']) && $paket['date'] != 'N/A') {
                        $date = new \DateTime($paket['date']);
                        $date->modify('+1 year');
                        $paket['date'] = $date->format('d.m.Y');
                    }
                }
            }
            $jsonLisanslar[] = $programlar;
        }

        // ═══════════════════════════════════════════════════════════
        // POS SYNC VERİLERİ
        // ═══════════════════════════════════════════════════════════
        $musteriLisansAnahtarlari = $Lisanslar->pluck('Anahtar')->filter()->toArray();

        $syncProducts   = collect();
        $syncFisler     = collect();
        $syncFaturalar  = collect();
        $syncCariler    = collect();
        $syncMasalar    = collect();
        $syncToplamCiro = 0;
        $patronlar      = collect();
		$syncYedekler   = collect();


        if (!empty($musteriLisansAnahtarlari)) {
            $syncProducts  = LicenseProduct::whereIn('license_key', $musteriLisansAnahtarlari)
                                        ->latest()->limit(100)->get();
            $syncFisler    = LicenseFis::whereIn('license_key', $musteriLisansAnahtarlari)
                                    ->latest('tarih')->limit(80)->get();
            $syncFaturalar = LicenseFatura::whereIn('license_key', $musteriLisansAnahtarlari)
                                        ->latest('tarih')->limit(80)->get();
            $syncCariler   = LicenseCari::whereIn('license_key', $musteriLisansAnahtarlari)
                                        ->latest()->limit(80)->get();
            $syncMasalar   = LicenseMasa::whereIn('license_key', $musteriLisansAnahtarlari)
                                        ->latest()->limit(80)->get();
            $syncToplamCiro = LicenseFis::whereIn('license_key', $musteriLisansAnahtarlari)->sum('toplam');
            $patronlar     = LicensePatron::whereIn('lisans_anahtar', $musteriLisansAnahtarlari)
                                ->orderBy('created_at', 'desc')->limit(50)->get();
			$syncYedekler  = LicenseYedek::whereIn('license_key', $musteriLisansAnahtarlari)
											->orderByDesc('yedek_tarihi')->limit(50)->get();
        }

        // 'lisansPaketler' değil 'LisansPaket' gönderiliyor
        return view('musteri.edit', compact(
            'Musteri', 'Lisanslar', 'saylisans', 'sayteklif', 'LisansPaket', 'jsonLisanslar',
            'syncProducts', 'syncFisler', 'syncFaturalar', 'syncCariler', 'syncMasalar', 'syncToplamCiro',
            'patronlar', 'musteriLisansAnahtarlari',  'syncYedekler'
			
        ));
    }

    /**
     * JSON verisi içinde (hem düz hem de gruplanmış halde) paketi arar.
     * Eski durumu (status, adet vb.) bulmak için kullanılır.
     *
     * @param array $programlar
     * @param string $targetName
     * @return array|null
     */
    private function findPackageInJson($programlar, $targetName)
    {
        if (!is_array($programlar)) {
            return null;
        }

        foreach ($programlar as $item) {
            // 1. Yazarkasa gibi gruplu yapı kontrolü
            if (isset($item['yazarkasa']) && is_array($item['yazarkasa'])) {
                foreach ($item['yazarkasa'] as $subItem) {
                    if (isset($subItem['paketName']) && $subItem['paketName'] == $targetName) {
                        return $subItem;
                    }
                }
            }

            // 2. Düz yapı kontrolü
            if (isset($item['paketName']) && $item['paketName'] == $targetName) {
                return $item;
            }
        }

        return null; // Bulunamazsa
    }

    public function updateWithSiparis(Request $request, $siparisNo)
    { 
        try {
            $user = Auth::user();

            // LOG BAŞLANGIÇ
            \Log::info("----------------------------------------------------------------");
            \Log::info("LİSANS GÜNCELLEME İŞLEMİ BAŞLADI (Kullanıcı ID: {$user->id})");

            // 1. Lisansı ve Mevcut Veriyi Çek
            $lisans = LisansModel::where('SiparisNo', $siparisNo)->first();

            if (!$lisans) {
                \Log::error("HATA: Lisans bulunamadı ($siparisNo)");
                return redirect()->back()->with('error', 'Lisans bulunamadı.')->with('run_error_js', true);
            }

            // Mevcut (Eski) Lisans Verisini Array'e Çevir
            $oldProgramlar = json_decode($lisans->Lisans, true);
            if (!is_array($oldProgramlar)) {
                $oldProgramlar = [];
            }

            // --- Fiyat Parametrelerini Hazırla ---
            $dbPaketler = LisansPaketModel::all()->keyBy('PaketName');

            // B) Bayinin İskonto Oranını Çek
            $indirimOrani = 0;
            if ($user->role != 'admin') {
                if (!$user->relationLoaded('bayi')) {
                    $user->load('bayi');
                }
                if ($user->bayi) {
                    $indirimOrani = $user->bayi->IndirimYuzdesi ?? 0;
                }
            }
            \Log::info("Bayi İskonto Oranı: %" . $indirimOrani);

            // C) Bayiye Özel Fiyatları Çek
            $ozelFiyatlar = [];
            $rawOzelFiyatlar = BayiOzelFiyatModel::where('user_id', $user->id)->get();
            foreach ($rawOzelFiyatlar as $of) {
                if ($of->paket) {
                    $ozelFiyatlar[$of->paket->PaketName] = $of->fiyat;
                }
            }
            \Log::info("Tanımlı Özel Fiyatlar:", $ozelFiyatlar);

            $paketler = [];
            if ($request->has('bulk_update')) {
                $paketler = $request->input('paketler', []);
            } else {
                $validated = $request->validate([
                    'paketName' => 'required|string',
                    'status' => 'required|boolean',
                    'date' => 'nullable|date',
                    'adet' => 'nullable|integer|min:0'
                ]);
                $paketler[] = $validated;
            }

            // 3. İşlem ve Hesaplama
            return DB::transaction(function () use ($paketler, $lisans, $oldProgramlar, $user, $dbPaketler, $indirimOrani, $ozelFiyatlar) {

                $totalCost = 0;
                $updatedProgramlar = $oldProgramlar;

                foreach ($paketler as $paket) {
                    if (!isset($paket['paketName']) || !isset($paket['status'])) {
                        continue;
                    }

                    $paketName = $paket['paketName'];
                    $newStatus = (int) $paket['status'];
                    $newAdet = isset($paket['adet']) ? (int) $paket['adet'] : 1;
                    $newDate = $paket['date'] ?? null;
                    $newSerials = isset($paket['serials']) ? $paket['serials'] : [];

                    // Eski durumu bul
                    $oldState = $this->findPackageInJson($oldProgramlar, $paketName);
                    $oldStatus = $oldState ? (int) $oldState['status'] : 0;
                    $oldAdet = ($oldState && isset($oldState['adet'])) ? (int) $oldState['adet'] : 0;
                    if ($oldStatus == 1 && $oldAdet == 0)
                        $oldAdet = 1;

                    \Log::info("--- Paket İşleniyor: $paketName ---");
                    \Log::info("Durum: $oldStatus -> $newStatus | Adet: $oldAdet -> $newAdet");

                    // --- BAKİYE HESAPLAMA MANTIĞI ---
                    if ($user->role != 'admin' && $lisans->Tipi == 'satis') {

                        $birimMaliyet = 0;
                        $paketModel = $dbPaketler->get($paketName);

                        if ($paketModel) {
                            // A) Özel Fiyat Varsa
                            if (array_key_exists($paketName, $ozelFiyatlar)) {
                                $birimMaliyet = (float) $ozelFiyatlar[$paketName];
                                \Log::info("Fiyat Kaynağı: ÖZEL FİYAT ($birimMaliyet TL)");
                            }
                            // B) İskonto Varsa
                            elseif ($indirimOrani > 0) {
                                $satisFiyati = $paketModel->PaketFiyati;
                                $birimMaliyet = $satisFiyati * (100 - $indirimOrani) / 100;
                                \Log::info("Fiyat Kaynağı: İSKONTOLU ($satisFiyati -> $birimMaliyet TL)");
                            }
                            // C) Standart Maliyet
                            else {
                                if ($user->is_main_dealer) {
                                    $birimMaliyet = $paketModel->AnaAlis;
                                    \Log::info("Fiyat Kaynağı: STANDART ANA BAYİ ALIŞ ($birimMaliyet TL)");
                                } else {
                                    $birimMaliyet = $paketModel->AltBayiAlis;
                                    \Log::info("Fiyat Kaynağı: STANDART ALT BAYİ ALIŞ ($birimMaliyet TL)");
                                }
                            }
                        } else {
                            \Log::warning("UYARI: Paket veritabanında bulunamadı: $paketName");
                        }

                        // Fark Hesaplama
                        if ($birimMaliyet > 0) {
                            $changeAmount = 0;

                            // 1. Pasif -> Aktif
                            if ($oldStatus == 0 && $newStatus == 1) {
                                $changeAmount = $birimMaliyet * $newAdet;
                                \Log::info("İşlem: Yeni Aktivasyon. Tutar: +$changeAmount");
                            }
                            // 2. Aktif -> Pasif
                            elseif ($oldStatus == 1 && $newStatus == 0) {
                                $changeAmount = -($birimMaliyet * $oldAdet);
                                \Log::info("İşlem: Pasife Alma (İade). Tutar: $changeAmount");
                            }
                            // 3. Aktif -> Aktif (Adet Değişimi)
                            elseif ($oldStatus == 1 && $newStatus == 1) {
                                $farkAdet = $newAdet - $oldAdet;
                                $changeAmount = $birimMaliyet * $farkAdet;
                                \Log::info("İşlem: Adet Değişimi ($farkAdet). Tutar: $changeAmount");
                            }

                            $totalCost += $changeAmount;
                        }
                    }

                    // JSON Güncelle
                    $this->updateSinglePacket(
                        $updatedProgramlar,
                        $paketName,
                        $newStatus,
                        $newDate,
                        $newAdet,
                        $newSerials
                    );
                }

                \Log::info("TOPLAM YANSIYACAK TUTAR: $totalCost TL");
                \Log::info("----------------------------------------------------------------");

                // 4. Bakiye İşlemi
                if ($totalCost != 0) {
                    $currentUser = User::lockForUpdate()->find($user->id);

                    if ($totalCost > 0 && $currentUser->Bakiye < $totalCost) {
                        \Log::error("Yetersiz Bakiye! Mevcut: {$currentUser->Bakiye}, İstenen: $totalCost");
                        throw new \Exception("Yetersiz Bakiye! İşlem tutarı: " . number_format($totalCost, 2) . " ₺, Mevcut: " . number_format($currentUser->Bakiye, 2) . " ₺");
                    }

                    $currentUser->decrement('Bakiye', $totalCost);

                    // Loglama
                    $logMessage = $totalCost > 0
                        ? 'Lisans güncelleme bedeli tahsil edildi.'
                        : 'Lisans güncelleme iadesi yapıldı.';

                    activity('Bakiye İşlemleri')
                        ->causedBy($user)
                        ->withProperties([
                            'tutar' => abs($totalCost),
                            'islem' => $totalCost > 0 ? 'Tahsilat' : 'İade',
                            'siparis_no' => $lisans->SiparisNo
                        ])
                        ->log($logMessage);
                }

                $lisans->Lisans = json_encode($updatedProgramlar);
                $lisans->save();

                $message = 'Lisanslar başarıyla güncellendi.';
                if ($totalCost > 0) {
                    $message .= ' Bakiyenizden ' . number_format($totalCost, 2) . ' ₺ düşüldü.';
                } elseif ($totalCost < 0) {
                    $message .= ' Bakiyenize ' . number_format(abs($totalCost), 2) . ' ₺ iade edildi.';
                }

                return redirect()->back()->with('success', $message)->with('run_success_js', true);
            });

        } catch (\Illuminate\Validation\ValidationException $e) {
            return redirect()->back()->with('warning', 'Formda eksik veya hatalı veriler var')->with('run_warning_js', true);
        } catch (\Exception $e) {
            return redirect()->back()->with('error', 'Bir hata oluştu: ' . $e->getMessage())->with('run_error_js', true);
        }
    }
    /**
     * Tek bir paketi günceller
     *
     * @param array &$programlar
     * @param string $paketName
     * @param int $status
     * @param string|null $date
     * @param int|null $adet
     * @param array $serials (YENİ)
     * @return bool
     */
    private function updateSinglePacket(&$programlar, $paketName, $status, $date = null, $adet = null, $serials = [])
    {
        $isYazarkasaPaket = in_array(strtolower($paketName), ['pavo', 'inpos', 'hugin']);
        $updated = false;

        foreach ($programlar as &$program) {
            // 1. Yazarkasa Grubu İçinde Ara
            if (isset($program['yazarkasa']) && is_array($program['yazarkasa'])) {
                foreach ($program['yazarkasa'] as &$yazarkasaPaket) {
                    if (isset($yazarkasaPaket['paketName']) && $yazarkasaPaket['paketName'] == $paketName) {
                        $yazarkasaPaket['status'] = $status;
                        if ($date !== null) {
                            $yazarkasaPaket['date'] = $date;
                        }
                        if ($adet !== null) {
                            $yazarkasaPaket['adet'] = $adet;
                        }
                        if (!empty($serials)) {
                            $yazarkasaPaket['serial_numbers'] = $serials;
                        }
                        $updated = true;
                        break 2;
                    }
                }

                // Yazarkasa grubu var ama bu paket içinde yoksa (Yeni Ekleme)
                if ($isYazarkasaPaket && !$updated) {
                    $newPaket = [
                        'paketName' => $paketName,
                        'status' => $status,
                        'date' => $date
                    ];
                    if ($adet !== null) {
                        $newPaket['adet'] = $adet;
                    }
                    if (!empty($serials)) {
                        $newPaket['serial_numbers'] = $serials;
                    }
                    $program['yazarkasa'][] = $newPaket;
                    $updated = true;
                    break;
                }
            }

            // 2. Düz Paketlerde Ara
            if (isset($program['paketName']) && $program['paketName'] == $paketName) {
                $program['status'] = $status;
                if ($date !== null) {
                    $program['date'] = $date;
                }
                if ($adet !== null) {
                    $program['adet'] = $adet;
                }
                // Düz paketlerde seri no varsa (nadiren)
                if (!empty($serials)) {
                    $program['serial_numbers'] = $serials;
                }
                $updated = true;
                break;
            }
        }

        // Eğer paket hiçbir yerde bulunamadıysa (Yeni Ekleme - En dışa)
        if (!$updated) {
            $foundYazarkasaArray = false;

            if ($isYazarkasaPaket) {
                // Mevcut 'yazarkasa' anahtarını bulmaya çalış
                foreach ($programlar as &$program) {
                    if (isset($program['yazarkasa']) && is_array($program['yazarkasa'])) {
                        $newPaket = [
                            'paketName' => $paketName,
                            'status' => $status,
                            'date' => $date
                        ];
                        if ($adet !== null) {
                            $newPaket['adet'] = $adet;
                        }
                        if (!empty($serials)) {
                            $newPaket['serial_numbers'] = $serials;
                        }
                        $program['yazarkasa'][] = $newPaket;
                        $foundYazarkasaArray = true;
                        $updated = true;
                        break;
                    }
                }

                // Hiç 'yazarkasa' grubu yoksa yeni oluştur
                if (!$foundYazarkasaArray) {
                    $newPaket = [
                        'paketName' => $paketName,
                        'status' => $status,
                        'date' => $date
                    ];
                    if ($adet !== null) {
                        $newPaket['adet'] = $adet;
                    }
                    if (!empty($serials)) {
                        $newPaket['serial_numbers'] = $serials;
                    }
                    $programlar[] = [
                        'yazarkasa' => [$newPaket]
                    ];
                    $updated = true;
                }
            } else {
                // Düz paket ekleme
                $newPaket = [
                    'paketName' => $paketName,
                    'status' => $status,
                    'date' => $date
                ];
                if ($adet !== null) {
                    $newPaket['adet'] = $adet;
                }
                if (!empty($serials)) {
                    $newPaket['serial_numbers'] = $serials;
                }
                $programlar[] = $newPaket;
                $updated = true;
            }
        }

        return $updated;
    }

    public function LisanceDown(Request $request, $SiparisNo)
    {
        try {
            $Dealer = LisansModel::where('SiparisNo', $SiparisNo)->first();
            $Dealer->update([
                'AnahtarKarsilik' => '',
                'Durum' => 0,
            ]);

            activity('Lisans İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Update';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['Siparis No' => $SiparisNo])
                ->log('Lisans Düşürüldü');

            return redirect()->back()->with('success', 'Lisans Başarılı Şekilde Düşürüldü')->with('run_success_js', true);
        } catch (\Exception $e) {
            return redirect()->back()->with('error', 'Güncelleme işlemi sırasında bir hata oluştu: ' . $e->getMessage())->with('run_error_js', true);
        }
    }

    public function LisanceUp(Request $request, $SiparisNo)
    {
        try {
            $Dealer = LisansModel::where('SiparisNo', $SiparisNo)->first();
            $Dealer->update([
                'Durum' => 1,
            ]);

            activity('Lisans İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Update';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['Siparis No' => $SiparisNo])
                ->log('Lisans Aktif Edildi');

            return redirect()->back()->with('success', 'Lisans Başarılı Şekilde Aktif Edildi Karşılık Gönderin')->with('run_success_js', true);
        } catch (\Exception $e) {
            return redirect()->back()->with('error', 'Güncelleme işlemi sırasında bir hata oluştu: ' . $e->getMessage())->with('run_error_js', true);
        }
    }

    public function agentLisanceUp(Request $request, $SiparisNo)
    {
        try {
            $Dealer = LisansModel::where('SiparisNo', $SiparisNo)->first();
            $Dealer->update([
                'Durum' => 1,
            ]);

            activity('Lisans İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Update';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['Siparis No' => $SiparisNo])
                ->log('Lisans Aktif Edildi');

            return redirect()->back()->with('success', 'Lisans Başarılı Şekilde Aktif Edildi Karşılık Gönderin')->with('run_success_js', true);
        } catch (\Exception $e) {
            return redirect()->back()->with('error', 'Güncelleme işlemi sırasında bir hata oluştu: ' . $e->getMessage())->with('run_error_js', true);
        }
    }

    public function agentLisanceDown(Request $request, $SiparisNo)
    {
        try {
            $Dealer = LisansModel::where('SiparisNo', $SiparisNo)->first();
            $Dealer->update([
                'AnahtarKarsilik' => '',
                'Durum' => 0,
            ]);

            activity('Lisans İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Update';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['Siparis No' => $SiparisNo])
                ->log('Lisans Düşürüldü');

            return redirect()->back()->with('success', 'Lisans Başarılı Şekilde Düşürüldü')->with('run_success_js', true);
        } catch (\Exception $e) {
            return redirect()->back()->with('error', 'Güncelleme işlemi sırasında bir hata oluştu: ' . $e->getMessage())->with('run_error_js', true);
        }
    }

    public function V1Customer(Request $request)
    {
        $Musteri = DB::connection('mysql2')->table('musteriler')->get();
        return view('musteri.v1', compact('Musteri'));
    }

    public function V1Change(Request $request, $id)
    {
        $Musteri = DB::connection('mysql2')->table('musteriler')->where('id', $id)->first();

        if ($Musteri->vergino) {
            $vergiKontrol = MusteriModel::where('VergiNo', $Musteri->vergino)->first();
            if ($vergiKontrol) {
                return redirect()->back()->with('error', 'Bu vergi numarasına sahip müşteri zaten sistemde kayıtlı!')->with('run_error_js', true);
            }
        }

        $yenimusteri = new MusteriModel();
        $yenimusteri->Bayi = Auth::user()->id;
        $yenimusteri->Ulke = $Musteri->Ulke;
        $yenimusteri->Il = 'Ordu';
        $yenimusteri->Ilce = 'Ünye';
        $yenimusteri->Unvan = $Musteri->adi;
        $yenimusteri->TabelaAdi = $Musteri->adi;
        $yenimusteri->Telefon = $Musteri->telefon;
        $yenimusteri->EMail = $Musteri->email;
        $yenimusteri->Adres = $Musteri->adres;
        $yenimusteri->VergiNo = $Musteri->vergino;
        $yenimusteri->VergiDairesi = $Musteri->vergidairesi;
        $yenimusteri->Yetkili = 'YOK';
        $yenimusteri->YetkiliGsm = 'YOK';
        $yenimusteri->save();

        DB::connection('mysql2')->table('musteriler')->where('id', $id)->delete();

        $Musteriyeni = urlencode(encrypt($yenimusteri->id));
        return redirect()->route('EditCustomer', ['id' => $Musteriyeni])->with('success', 'Müşteri Başarılı Şekilde Oluşturuldu ve Kaynak Sistemden Silindi. Eksikleri Doldur')->with('run_success_js', true);
    }
}