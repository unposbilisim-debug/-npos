<?php

namespace App\Http\Controllers;

use App\Models\LisansModel;
use App\Models\MusteriModel;
use App\Models\LisansPaketModel;
use App\Models\User;
use App\Models\BayiOzelFiyatModel;
use Illuminate\Http\Request;
use Illuminate\Support\Str;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\DB;
use Illuminate\Contracts\Encryption\DecryptException;
use Carbon\Carbon;
use Illuminate\Support\Facades\Http;

class LisansController extends Controller
{
    // ============================================================================
    // YARDIMCI FONKSİYONLAR (Private Helpers)
    // ============================================================================

    /**
     * Lisans JSON verisini işler, kalan günleri hesaplar ve sıralar.
     */
    private function processLicenseData($lisanslar)
    {
        $bugun = Carbon::now()->startOfDay();
        $processedList = [];

        foreach ($lisanslar as $lisans) {
            $lisansData = json_decode($lisans->Lisans, true);
            if (!is_array($lisansData))
                continue;

            // Müşteri adını bir kere çekelim
            $musteriAdi = $lisans->musterix ? $lisans->musterix->Unvan : 'Bilinmiyor';

            foreach ($lisansData as $key => $item) {
                // 1. Durum: Düz Paket
                if (is_array($item) && isset($item['date']) && isset($item['paketName'])) {
                    $this->addItemToProcessedList($processedList, $item, $lisans, $musteriAdi, $bugun);
                }
                // 2. Durum: Gruplu Paket (Örn: yazarkasa, modul vb.)
                elseif (is_array($item)) {
                    foreach ($item as $subItem) {
                        if (is_array($subItem) && isset($subItem['date']) && isset($subItem['paketName'])) {
                            $this->addItemToProcessedList($processedList, $subItem, $lisans, $musteriAdi, $bugun);
                        }
                    }
                }
            }
        }

        // Kalan güne göre sırala (Az kalan en üstte)
        usort($processedList, function ($a, $b) {
            return $a['kalanGun'] - $b['kalanGun'];
        });

        return $processedList;
    }

    /**
     * Tekil lisans öğesini işlenmiş listeye ekler.
     */
    private function addItemToProcessedList(&$list, $item, $lisans, $musteriAdi, $bugun)
    {
        try {
            $tarih = trim($item['date']);
            // Tarih formatı kontrolü (d.m.Y)
            if (!preg_match('/^\d{2}\.\d{2}\.\d{4}$/', $tarih))
                return;

            $bitisTarihi = Carbon::createFromFormat('d.m.Y', $tarih)->startOfDay();
            $kalanGun = (int) $bugun->diffInDays($bitisTarihi, false);

            $list[] = [
                'lisansId' => $lisans->id,
                'siparisNo' => $lisans->SiparisNo,
                'musteriId' => $lisans->Musteri,
                'musteri' => $musteriAdi,
                'paketName' => $item['paketName'],
                'bitisTarihi' => $item['date'],
                'kalanGun' => $kalanGun,
                'status' => $item['status'] ?? 0,
                'anahtar' => $lisans->Anahtar,
                'anahtarKarsilik' => $lisans->AnahtarKarsilik,
                'pcName' => $lisans->PcName,
                'serial_numbers' => $item['serial_numbers'] ?? [] // Seri Numaralarını da listeye ekledik
            ];
        } catch (\Exception $e) {
            // Hata durumunda bu kaydı atla
        }
    }

    /**
     * Kullanıcının yetkisine göre görebileceği Bayi ID'lerini döndürür.
     */
    private function getAccessibleDealerIds()
    {
        $user = Auth::user();
        $ids = [$user->id];

        if ($user->is_main_dealer) {
            $subIds = User::where('parent_id', $user->id)->pluck('id')->toArray();
            $ids = array_merge($ids, $subIds);
        }

        return $ids;
    }

    // ============================================================================
    // VIEW CONTROLLERS (Sayfa Gösterimi)
    // ============================================================================

    public function showLicensePage(Request $request, $id, $isAgent = false)
    {
        try {
            $id = decrypt(urldecode($id));
            $Musteri = MusteriModel::find($id);

            if (!$Musteri)
                abort(404, 'Müşteri Bulunamadı');

            // Bayi ise yetki kontrolü yap
            if ($isAgent) {
                $user = Auth::user();
                $isOwner = $Musteri->Bayi == $user->id;
                // Ana bayi alt bayisinin müşterisini görebilir
                $isParent = $user->is_main_dealer && User::where('id', $Musteri->Bayi)->where('parent_id', $user->id)->exists();

                if (!$isOwner && !$isParent) {
                    abort(403, 'Bu müşteriye işlem yapma yetkiniz yok.');
                }
            }

            $LisansPaket = LisansPaketModel::all();

            // --- KRİTİK DÜZELTME: Özel Fiyatları Çekme Mantığı ---
            // Satışı yapan kişinin (Oturum açan kullanıcının) özel fiyatları geçerli olmalı.
            // Çünkü parayı o ödüyor. Müşterinin bağlı olduğu alt bayi değil.
            $dealerId = Auth::id();

            $ozelFiyatlar = BayiOzelFiyatModel::where('user_id', $dealerId)
                ->pluck('fiyat', 'paket_id')
                ->toArray();
            // ----------------------------------------------------

            $rawLicenses = LisansModel::where('Musteri', $id)->get();
            $musteriLisanslari = $this->processLicenseData($rawLicenses);
            $grupluLisanslar = collect($musteriLisanslari)->groupBy('musteriId');

            return view('lisans.index', compact('Musteri', 'LisansPaket', 'musteriLisanslari', 'grupluLisanslar', 'ozelFiyatlar'));

        } catch (DecryptException $e) {
            abort(404, 'Geçersiz ID');
        }
    }

    public function License(Request $request, $id)
    {
        return $this->showLicensePage($request, $id, false); // Admin
    }

    public function agentLicense(Request $request, $id)
    {
        return $this->showLicensePage($request, $id, true); // Agent
    }

    /**
     * Tüm Lisanslar Listesi (Dashboard -> Tümünü Gör)
     */
    public function AllLicenses(Request $request)
    {
        $user = Auth::user();

        if ($user->role == 'admin') {
            $lisanslar = LisansModel::with('bayilers', 'musterix')->get();
        } else {
            $ids = $this->getAccessibleDealerIds();
            $lisanslar = LisansModel::with('bayilers', 'musterix')->whereIn('Bayi', $ids)->get();
        }

        $processedList = $this->processLicenseData($lisanslar);
        $grupluLisanslar = collect($processedList)->groupBy('musteriId');

        return view('lisans.AllLicenses', compact('grupluLisanslar'));
    }

    /**
     * Yaklaşan Lisanslar Listesi
     */
    public function UpcomingLicenses(Request $request)
    {
        $user = Auth::user();

        if ($user->role == 'admin') {
            $lisanslar = LisansModel::with('bayilers', 'musterix')->get();
        } else {
            $ids = $this->getAccessibleDealerIds();
            $lisanslar = LisansModel::with('bayilers', 'musterix')->whereIn('Bayi', $ids)->get();
        }

        $allProcessed = $this->processLicenseData($lisanslar);

        $gun = (int) $request->query('gun', 30);
        if ($gun < 1 || $gun > 365) {
            $gun = 30;
        }

        $yaklasakList = array_filter($allProcessed, function ($item) use ($gun) {
            return $item['kalanGun'] <= $gun && $item['kalanGun'] >= -$gun;
        });

        $grupluLisanslar = collect($yaklasakList)->groupBy('musteriId');

        return view('lisans.AllLicenses', compact('grupluLisanslar'));
    }

    // ============================================================================
    // ACTION CONTROLLERS (İşlem Yapma)
    // ============================================================================

    /**
     * Yeni Lisans/Teklif Ekleme
     */
    public function AddLicense(Request $request)
    {
        return $this->handleLicenseCreation($request);
    }
    public function agentAddLicense(Request $request)
    {
        return $this->handleLicenseCreation($request);
    }

    private function handleLicenseCreation(Request $request)
    {
        try {
            $paketler = json_decode($request->input('paketler'), true);

            if (is_null($paketler) || !is_array($paketler)) {
                return redirect()->back()->with('error', 'Paket seçilmedi veya veri hatalı.');
            }

            $user = Auth::user();
            $isSale = $request->input('sale_type') == 'satis';
            $toplamAlis = floatval($request->input('toplam_alis_fiyati'));
            $musteriUnvan = $request->input('musteri_unvan');

            // --- QR MENU ENTEGRASYONU ---
            $qrMenuData = null;
            $hasQrMenu = false;

            // Paketler arasında qrmenu var mı kontrol et
            foreach ($paketler as $p) {
                if (isset($p['PaketName']) && strtolower($p['PaketName']) === 'qrmenu') {
                    $hasQrMenu = true;
                    break;
                }
            }

            // Varsa API isteğini hazırla (Henüz gönderme)
            if ($hasQrMenu) {
                $qrEmail = $request->input('qr_email');
                $qrPassword = $request->input('qr_password');

                if (empty($qrEmail) || empty($qrPassword)) {
                    return redirect()->back()->with('error', 'QR Menü için e-posta ve şifre zorunludur.');
                }

                $qrMenuData = [
                    'name' => $request->input('musteri_unvan'),
                    'email' => $qrEmail,
                    'password' => $qrPassword,
                    'dealer' => $user->name, // Bayi adı
                    'licenseEndDate' => Carbon::now()->addYear()->format('Y-m-d'),
                    'notes' => 'Oluşturan Bayi: ' . $user->name . ' (ID: ' . $user->id . ')'
                ];
            }
            // ---------------------------

            // Transaction başlat
            DB::transaction(function () use ($request, $paketler, $user, $isSale, $toplamAlis, $qrMenuData, $hasQrMenu, $musteriUnvan) {

                // 1. Bakiye Kontrolü ve Düşümü
                if ($user->role != 'admin' && $isSale) {
                    $bayi = User::lockForUpdate()->find($user->id);

                    if ($bayi->Bakiye < $toplamAlis) {
                        throw new \Exception('Yetersiz bakiye! İşlem için bakiyeniz yetersiz.');
                    }

                    $bayi->decrement('Bakiye', $toplamAlis);

                    activity('Bakiye İşlemleri')
                        ->causedBy($user)
                        ->withProperties(['tutar' => $toplamAlis, 'islem' => 'Lisans Satışı'])
                        ->log('Satış tutarı bakiyeden düşüldü');
                }

                // 2. QR Menu API Çağrısı
                if ($hasQrMenu && $isSale) {
                    $apiUrl = env('QR_MENU_API_URL');
                    $apiKey = env('QR_MENU_SECRET_KEY');

                    // DEĞİŞİKLİK BURADA: withoutVerifying() eklendi.
                    $response = Http::withoutVerifying()
                        ->withHeaders([
                            'x-integration-key' => $apiKey,
                            'Content-Type' => 'application/json'
                        ])->post($apiUrl, $qrMenuData);

                    if ($response->failed()) {
                        $errorMsg = $response->json()['error'] ?? 'Bilinmeyen API Hatası (' . $response->status() . ')';
                        throw new \Exception("QR Menü Hesabı Oluşturulamadı: " . $errorMsg);
                    }
                }

                // 3. Yerel Lisans Kaydı
                $formattedPaketler = [];
                $yazarkasaPakets = [];
                $logPaketIsimleri = [];

                foreach ($paketler as $paket) {
                    $paketModel = LisansPaketModel::find($paket['id']);
                    $paketName = $paketModel ? $paketModel->PaketName : ($paket['PaketName'] ?? 'Bilinmeyen');
                    $paketTipi = $paketModel ? $paketModel->PaketTipi : ($paket['type'] ?? 'unknown');
                    $quantity = $paket['quantity'] ?? 1;
                    $date = Carbon::now()->addYear()->format('d.m.Y');

                    $itemData = [
                        'paketName' => $paketName,
                        'status' => 1,
                        'date' => $date,
                        'adet' => $quantity
                    ];

                    if (strtolower($paketTipi) === 'yazarkasa') {
                        $itemData['serial_numbers'] = $paket['serials'] ?? [];
                        $yazarkasaPakets[] = $itemData;
                    } else {
                        $formattedPaketler[] = $itemData;
                    }
                }

                if (!empty($yazarkasaPakets)) {
                    $formattedPaketler[] = ['yazarkasa' => $yazarkasaPakets];
                }

                $finalJson = json_encode($formattedPaketler);
                $siparisNo = 'ORD-' . time() . rand(1000, 9999);

                $pcNames = $request->input('pcname', []);
                if (!is_array($pcNames))
                    $pcNames = [$pcNames];
                if (empty($pcNames))
                    $pcNames = ['PC-1'];

                $lastLisansObj = null;

                foreach ($pcNames as $pcName) {
                    if (empty($pcName))
                        continue;

                    $Lisans = new LisansModel();
                    $Lisans->Bayi = $user->id;
                    $Lisans->Musteri = $request->input('musteri_id');
                    $Lisans->SiparisNo = $siparisNo;
                    $Lisans->Lisans = $finalJson;
                    $Lisans->Mesaj = "Merhaba, lisansınız aktif edilmiştir.";
                    $Lisans->PcName = $pcName;
                    $Lisans->Durum = '1';
                    $Lisans->Anahtar = Str::upper(Str::random(4) . '-' . Str::random(4) . '-' . Str::random(4) . '-' . Str::random(4));
                    $Lisans->AnahtarKarsilik = '';
                    $Lisans->Tipi = $request->input('sale_type');
                    $Lisans->save();

                    $lastLisansObj = $Lisans;
                }

                $islemMetni = $isSale ? 'Yeni Lisans Satışı' : 'Lisans Teklifi Oluşturuldu';

                activity('Lisans İşlemleri')
                    ->causedBy($user)
                    ->performedOn($lastLisansObj)
                    ->withProperties([
                        'islem' => $islemMetni,
                        'siparis_no' => $siparisNo,
                        'musteri' => $musteriUnvan,
                        'paketler' => implode(', ', $logPaketIsimleri),
                        'cihaz_sayisi' => count($pcNames),
                        'tutar' => $isSale ? $toplamAlis : 0,
                        'durum' => $isSale ? 'Satıldı' : 'Teklif'
                    ])
                    ->log($islemMetni . ': ' . $siparisNo);
            }, 2); // Deadlock retry

            return redirect()->back()->with('success', 'İşlem başarıyla tamamlandı.');

        } catch (\Exception $e) {
            return redirect()->back()->with('error', $e->getMessage())->withInput();
        }
    }

    /**
     * Lisans Süresi Uzatma (Modal'dan Gelen)
     */
    public function ExtendLicense(Request $request)
    {
        $request->validate([
            'lisansId' => 'required',
            'paketName' => 'required',
            'newExpirationDate' => 'required|date'
        ]);

        try {
            $lisans = LisansModel::findOrFail($request->lisansId);
            $lisansData = json_decode($lisans->Lisans, true);
            $targetPaketName = trim($request->paketName);
            $newDate = Carbon::parse($request->newExpirationDate)->format('d.m.Y');

            $updated = false;

            // Recursive fonksiyon ile güncelleme
            $updateRecursive = function (&$data) use ($targetPaketName, $newDate, &$updated, &$updateRecursive) {
                foreach ($data as $key => &$item) {
                    if (is_array($item)) {
                        // Düz paket kontrolü
                        if (isset($item['paketName']) && trim($item['paketName']) == $targetPaketName) {
                            $item['date'] = $newDate;
                            $item['status'] = 1;
                            $updated = true;
                        }
                        // Yazarkasa gibi alt arrayler içinde ara
                        else {
                            $updateRecursive($item);
                        }
                    }
                }
            };

            $updateRecursive($lisansData);

            if ($updated) {
                $lisans->Lisans = json_encode($lisansData);
                $lisans->save();

                activity('Lisans İşlemleri')
                    ->causedBy(Auth::user())
                    ->withProperties(['lisans_id' => $lisans->id])
                    ->log('Lisans süresi uzatıldı');

                return redirect()->back()->with('success', 'Lisans süresi başarıyla uzatıldı.');
            }

            return redirect()->back()->with('error', 'Paket bulunamadı veya güncellenemedi.');

        } catch (\Exception $e) {
            return redirect()->back()->with('error', 'Hata: ' . $e->getMessage());
        }
    }

    /**
     * Lisans Paketlerini Güncelleme (Ekleme/Çıkarma/Süre Uzatma)
     * Fiyatlandırma ve Bakiye İade/Düşüm Mantığı Burada Çalışır.
     */
    public function updateWithSiparis(Request $request, $siparisNo)
    {
        try {
            $user = Auth::user();

            // 1. Lisansı ve Mevcut Veriyi Çek
            $lisans = LisansModel::where('SiparisNo', $siparisNo)->first();

            if (!$lisans) {
                return redirect()->back()->with('error', 'Lisans bulunamadı.')->with('run_error_js', true);
            }

            // Mevcut (Eski) Lisans Verisini Array'e Çevir
            $oldProgramlar = json_decode($lisans->Lisans, true);
            if (!is_array($oldProgramlar)) {
                $oldProgramlar = [];
            }

            // --- FİYAT VE PARAMETRE HAZIRLIĞI ---

            // A) Tüm Paketlerin Veritabanı Bilgilerini Çek (Fiyatlar için)
            $dbPaketler = LisansPaketModel::all()->keyBy('PaketName');

            // B) Bayinin İskonto Oranını Çek (Admin Değilse)
            $indirimOrani = 0;
            if ($user->role != 'admin') {
                if (!$user->relationLoaded('bayi')) {
                    $user->load('bayi');
                }
                if ($user->bayi) {
                    $indirimOrani = $user->bayi->IndirimYuzdesi ?? 0;
                }
            }

            // C) Bayiye Özel Fiyatları Çek [paketName => fiyat]
            $ozelFiyatlar = [];
            // İşlemi yapan bayinin (Auth::user()) özel fiyatlarını alıyoruz
            $rawOzelFiyatlar = BayiOzelFiyatModel::where('user_id', $user->id)->get();
            foreach ($rawOzelFiyatlar as $of) {
                if ($of->paket) {
                    $ozelFiyatlar[$of->paket->PaketName] = $of->fiyat;
                }
            }

            // D) Gelen Veriyi Hazırla
            $paketler = [];
            if ($request->has('bulk_update')) {
                $paketler = $request->input('paketler', []);
            } else {
                // Tekil güncelleme desteği (opsiyonel, eski yapı için)
                $validated = $request->validate([
                    'paketName' => 'required|string',
                    'status' => 'required|boolean',
                    'date' => 'nullable|date',
                    'adet' => 'nullable|integer|min:0'
                ]);
                $paketler[] = $validated;
            }

            $pcName = trim((string) $request->input('pc_name', ''));
            $pcName = preg_replace('/\s+/u', ' ', $pcName);
            if (mb_strlen($pcName) > 48) {
                $pcName = mb_substr($pcName, 0, 48);
            }

            // 3. İŞLEM VE HESAPLAMA (Transaction Başlangıcı)
            return DB::transaction(function () use ($paketler, $lisans, $oldProgramlar, $user, $dbPaketler, $indirimOrani, $ozelFiyatlar, $pcName) {

                $totalCost = 0; // Toplam Maliyet Farkı (Pozitif: Borç, Negatif: İade)
                $updatedProgramlar = $oldProgramlar;

                foreach ($paketler as $paket) {
                    if (!isset($paket['paketName']))
                        continue; // Status bazen gelmeyebilir, kontrolü aşağıda yapalım

                    $paketName = $paket['paketName'];
                    $newStatus = (int) ($paket['status'] ?? 0);
                    $newAdet = isset($paket['adet']) ? (int) $paket['adet'] : 1;
                    $newDate = $paket['date'] ?? null;

                    // Seri Numaralarını Al
                    $newSerials = isset($paket['serials']) ? $paket['serials'] : [];

                    // Bu paketin ESKİ halini bul (Fiyat farkı hesaplamak için)
                    $oldState = $this->findPackageInJson($oldProgramlar, $paketName);
                    $oldStatus = $oldState ? (int) ($oldState['status'] ?? 0) : 0;
                    $oldAdet = ($oldState && isset($oldState['adet'])) ? (int) $oldState['adet'] : 0;

                    // Eğer paket önceden aktifse ama adeti yoksa (eski veri), 1 sayalım
                    if ($oldStatus == 1 && $oldAdet == 0)
                        $oldAdet = 1;

                    // --- BAKİYE HESAPLAMA MANTIĞI ---
                    // Sadece Admin değilse ve Satış tipindeyse hesapla
                    if ($user->role != 'admin' && $lisans->Tipi == 'satis') {

                        $birimMaliyet = 0;
                        $paketModel = $dbPaketler->get($paketName);

                        if ($paketModel) {
                            // 1. Kural: Özel Fiyat Varsa -> Onu Kullan
                            if (array_key_exists($paketName, $ozelFiyatlar)) {
                                $birimMaliyet = (float) $ozelFiyatlar[$paketName];
                            }
                            // 2. Kural: Özel Fiyat Yok ama İskonto Varsa -> Satış Fiyatından İskonto Düş
                            elseif ($indirimOrani > 0) {
                                $satisFiyati = $paketModel->PaketFiyati;
                                $birimMaliyet = $satisFiyati * (100 - $indirimOrani) / 100;
                            }
                            // 3. Kural: Hiçbiri Yoksa -> Standart Alış (Maliyet) Fiyatları
                            else {
                                if ($user->is_main_dealer) {
                                    $birimMaliyet = $paketModel->AnaAlis;
                                } else {
                                    $birimMaliyet = $paketModel->AnaAlis;
                                }
                            }
                        }

                        // Fark Hesaplama (Eğer maliyet > 0 ise)
                        if ($birimMaliyet > 0) {
                            // Durum 1: Pasif -> Aktif (Tam Ücret Ekle)
                            if ($oldStatus == 0 && $newStatus == 1) {
                                $totalCost += $birimMaliyet * $newAdet;
                            }
                            // Durum 2: Aktif -> Pasif (Tam Ücret İade)
                            elseif ($oldStatus == 1 && $newStatus == 0) {
                                $totalCost -= $birimMaliyet * $oldAdet;
                            }
                            // Durum 3: Aktif -> Aktif (Adet Değişimi: Fark Ekle/Çıkar)
                            elseif ($oldStatus == 1 && $newStatus == 1) {
                                $farkAdet = $newAdet - $oldAdet;
                                $totalCost += $birimMaliyet * $farkAdet; // Pozitifse borç, negatifse iade olur
                            }
                        }
                    }

                    // --- JSON GÜNCELLEME ---
                    $this->updateSinglePacket(
                        $updatedProgramlar,
                        $paketName,
                        $newStatus,
                        $newDate,
                        $newAdet,
                        $newSerials
                    );
                }

                // 4. Bakiye Kontrolü ve İşlemi
                if ($totalCost != 0) {
                    $currentUser = User::lockForUpdate()->find($user->id);

                    // Eğer BORÇ çıkıyorsa ve bakiye yetersizse
                    if ($totalCost > 0) {
                        if ($currentUser->Bakiye < $totalCost) {
                            throw new \Exception("Yetersiz Bakiye! İşlem tutarı: " . number_format($totalCost, 2) . " ₺, Mevcut: " . number_format($currentUser->Bakiye, 2) . " ₺");
                        }
                        $currentUser->decrement('Bakiye', $totalCost);
                    }
                    // Eğer İADE çıkıyorsa (Negatif Tutar)
                    else {
                        $iadeTutari = abs($totalCost); // Mutlak değer al
                        $currentUser->increment('Bakiye', $iadeTutari);
                    }

                    // Loglama
                    $islemTipi = $totalCost > 0 ? 'Tahsilat' : 'İade';
                    $logMessage = $totalCost > 0
                        ? 'Lisans güncelleme bedeli tahsil edildi.'
                        : 'Lisans güncelleme iadesi yapıldı.';

                    activity('Bakiye İşlemleri')
                        ->causedBy($user)
                        ->withProperties([
                            'tutar' => abs($totalCost),
                            'islem' => $islemTipi,
                            'siparis_no' => $lisans->SiparisNo
                        ])
                        ->log($logMessage);
                }

                // 5. Kaydetme
                $lisans->Lisans = json_encode($updatedProgramlar);
                if ($pcName !== '') {
                    $lisans->PcName = $pcName;
                }
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
     * YARDIMCI: JSON verisi içinde (hem düz hem de gruplanmış halde) paketi arar.
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

    /**
     * Tek bir paketi günceller - Mevcut veri yapısını koruyarak
     * @param array
     * @param string $paketName
     * @param int $status
     * @param string|null $date
     * @param int|null $adet
     * @param array $serials (YENİ PARAMETRE)
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
                        // SERİ NO GÜNCELLEME
                        if (!empty($serials)) {
                            $yazarkasaPaket['serial_numbers'] = $serials;
                        }
                        $updated = true;
                        break 2;
                    }
                }

                // Yazarkasa grubu var ama bu paket içinde yoksa ekle (Yeni ekleme durumu)
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
                // Düz paketlerde seri no genellikle olmaz ama istenirse eklenebilir
                if (!empty($serials)) {
                    $program['serial_numbers'] = $serials;
                }
                $updated = true;
                break;
            }
        }

        // Eğer paket hiçbir yerde bulunamadıysa (Yeni Ekleme)
        if (!$updated) {
            $foundYazarkasaArray = false;

            if ($isYazarkasaPaket) {
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
                    $program['yazarkasa'][] = $newPaket;
                    $updated = true;
                }
            } else {
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

    // Durum Değiştirme (Aktif/Pasif)
    public function LisanceUp(Request $request, $SiparisNo)
    {
        return $this->toggleStatus($SiparisNo, 1);
    }
    public function LisanceDown(Request $request, $SiparisNo)
    {
        return $this->toggleStatus($SiparisNo, 0);
    }
    public function agentLisanceUp(Request $request, $SiparisNo)
    {
        return $this->toggleStatus($SiparisNo, 1);
    }
    public function agentLisanceDown(Request $request, $SiparisNo)
    {
        return $this->toggleStatus($SiparisNo, 0);
    }

    private function toggleStatus($SiparisNo, $status)
    {
        try {
            $lisans = LisansModel::where('SiparisNo', $SiparisNo)->firstOrFail();

            // Bayi Yetki Kontrolü
            if (Auth::user()->role != 'admin') {
                // Sadece kendi lisansına müdahale edebilir mi kontrolü eklenebilir
            }

            $lisans->update(['Durum' => $status]);

            $msg = $status == 1 ? 'Lisans Aktif Edildi' : 'Lisans Pasife Alındı';
            return back()->with('success', $msg);
        } catch (\Exception $e) {
            return back()->with('error', 'Hata: ' . $e->getMessage());
        }
    }

    /**
     * Log ekranından çağrılan Sipariş Detay API'si
     */
    public function getLicenseDetailsByOrder(Request $request, $siparisNo)
    {
        // Yetki kontrolü (İsteğe bağlı sıkılaştırılabilir)
        if (!Auth::check()) {
            return response()->json(['error' => 'Yetkisiz işlem'], 403);
        }

        $lisans = LisansModel::with(['musterix', 'bayilers'])
            ->where('SiparisNo', $siparisNo)
            ->first();

        if (!$lisans) {
            return response()->json(['error' => 'Kayıt bulunamadı'], 404);
        }

        // JSON verisini decode edip okunabilir hale getirelim
        $icerik = json_decode($lisans->Lisans, true);

        return response()->json([
            'html' => view('lisans.log-modal-content', compact('lisans', 'icerik'))->render()
        ]);
    }

    /**
     * Paket Satış Fiyat Listesi (Katalog)
     */
    public function showPriceList()
    {
        // Sadece aktif paketleri, belirlenen sırada çekiyoruz
        $LisansPaket = LisansPaketModel::where('PaketDurum', 1)
            ->orderBy('PaketSira', 'asc')
            ->get();

        return view('lisans.price_list', compact('LisansPaket'));
    }

    /**
     * Admin Yazar Kasa Raporu
     */
    public function YazarKasaReport()
    {
        // 1. Güvenlik Kontrolü: Sadece Admin
        if (Auth::user()->role !== 'admin') {
            abort(403, 'Bu sayfaya erişim yetkiniz yok.');
        }

        // 2. Tüm Lisansları Çek (Müşteri ve Bayi bilgileriyle)
        $lisanslar = LisansModel::with(['musterix', 'bayilers'])->get();

        $raporVerisi = [];

        foreach ($lisanslar as $lisans) {
            $jsonData = json_decode($lisans->Lisans, true);

            if (!is_array($jsonData))
                continue;

            // Müşteri Bilgileri
            $musteri = $lisans->musterix;
            $bayi = $lisans->bayilers;

            // JSON verisini tarayalım — hem 'yazarkasa' grubu hem düz yazar kasa paketleri
            foreach ($jsonData as $key => $item) {
                if (!is_array($item))
                    continue;

                // Yapı 1: 'yazarkasa' anahtarı altında gruplanmış (büyük/küçük harf fark etmez)
                $ykGrup = null;
                foreach ($item as $k => $v) {
                    if (is_string($k) && strtolower($k) === 'yazarkasa' && is_array($v)) {
                        $ykGrup = $v;
                        break;
                    }
                }
                if (is_string($key) && strtolower($key) === 'yazarkasa' && is_array($item)) {
                    $ykGrup = $item;
                }

                if (is_array($ykGrup)) {
                    foreach ($ykGrup as $ykItem) {
                        if (is_array($ykItem) && isset($ykItem['date'])) {
                            $this->addYazarKasaToReport($raporVerisi, $ykItem, $musteri, $bayi);
                        }
                    }
                    continue;
                }

                // Yapı 2: Düz paket ama yazar kasa — ayırt edici işaret: serial_numbers dolu
                if (isset($item['paketName']) && !empty($item['serial_numbers']) && is_array($item['serial_numbers'])) {
                    $this->addYazarKasaToReport($raporVerisi, $item, $musteri, $bayi);
                }
            }
        }

        // Tarihe göre sıralama (Bitiş tarihi en yakın olan en üstte)
        usort($raporVerisi, function ($a, $b) {
            return strtotime($a['bitis_tarihi_raw']) - strtotime($b['bitis_tarihi_raw']);
        });

        return view('lisans.yazarkasa_rapor', compact('raporVerisi'));
    }

    /**
     * Rapor dizisine veri ekleyen yardımcı fonksiyon
     */
    private function addYazarKasaToReport(&$list, $item, $musteri, $bayi)
    {
        // Tarih parse etme
        $bitisStr = $item['date'] ?? null;
        if (!$bitisStr)
            return;

        try {
            $bitisStr = trim($bitisStr);
            // JSON'da tarih hem "Y-m-d" hem "d.m.Y" gelebilir — ikisini de destekle
            if (preg_match('/^\d{4}-\d{2}-\d{2}$/', $bitisStr)) {
                $bitis = Carbon::createFromFormat('Y-m-d', $bitisStr)->startOfDay();
            } else {
                $bitis = Carbon::createFromFormat('d.m.Y', $bitisStr)->startOfDay();
            }

            $baslangic = $bitis->copy()->subYear();

            $bugun = Carbon::now()->startOfDay();
            $kalanGun = (int) $bugun->diffInDays($bitis, false);

            $marka = $item['paketName'] ?? 'Belirsiz';
            $serials = $item['serial_numbers'] ?? [];

            if (empty($serials)) {
                $serials = ['Belirtilmemiş'];
            }

            foreach ($serials as $seriNo) {
                $list[] = [
                    'bayi' => $bayi ? $bayi->name : 'Silinmiş Bayi',
                    'musteri' => $musteri ? $musteri->Unvan : 'Silinmiş Müşteri',
                    'yetkili' => $musteri ? $musteri->Yetkili : '-',
                    'telefon' => $musteri ? ($musteri->Telefon ?? $musteri->YetkiliGsm) : '-',
                    'marka' => $marka,
                    'seri_no' => $seriNo,
                    'baslangic_tarihi' => $baslangic->format('d.m.Y'),
                    'bitis_tarihi' => $bitis->format('d.m.Y'),
                    'bitis_tarihi_raw' => $bitis->format('Y-m-d'),
                    'kalan_gun' => (int) $kalanGun
                ];
            }

        } catch (\Exception $e) {
            // Tarih formatı hatalıysa atla
            return;
        }
    }
}