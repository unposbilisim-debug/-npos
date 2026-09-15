<?php

use Illuminate\Support\Facades\Route;
use App\Http\Controllers\DosyaSistemi;
use App\Http\Controllers\LogController;
use App\Http\Controllers\BayiController;
use App\Http\Controllers\AdminController;
use App\Http\Controllers\AgentController;
use App\Http\Controllers\OdemeController;
use App\Http\Controllers\PayTRController;
use App\Http\Controllers\LisansController;
use App\Http\Controllers\TeklifController;
use App\Http\Controllers\AyarlarController;
use App\Http\Controllers\MusteriController;

use App\Http\Controllers\ProfileController;
use App\Http\Controllers\BildirimlerController;
use App\Http\Controllers\SozlesmelerController;
use App\Http\Controllers\ProgramPaketleriController;
use App\Http\Controllers\PatronController;
use App\Http\Controllers\Api\SyncExportController;
use App\Http\Controllers\GuncelleController;

Route::get('/', function () {
    return view('auth.login');
});

// Mobil Patron (bulut köprüsü) — auth yok; lisans anahtarı + mağaza PIN
Route::get('/patron', function () {
    return view('patron-kur');
})->name('patron.kur');

Route::get('/patron-mobil', function () {
    return view('patron-mobil');
})->name('patron.mobil');

Route::get('/boss-mobil', function () {
    return view('boss-mobil');
})->name('boss.mobil');

Route::get('/patron-pwa/manifest.webmanifest', function () {
    return response()
        ->file(public_path('patron-pwa/manifest.webmanifest'), [
            'Content-Type' => 'application/manifest+json',
            'Cache-Control' => 'public, max-age=3600',
        ]);
});

Route::get('/dashboard', function () {
    $user = auth()->user();
    if ($user && ($user->role ?? null) === 'agent') {
        return redirect()->route('agent.dashboard');
    }

    return redirect()->route('Desk');
})->middleware(['auth', 'verified'])->name('dashboard');

Route::middleware('auth')->group(function () {
    Route::get('/profile', [ProfileController::class, 'edit'])->name('profile.edit');
    Route::patch('/profile', [ProfileController::class, 'update'])->name('profile.update');
    Route::delete('/profile', [ProfileController::class, 'destroy'])->name('profile.destroy');
});

Route::middleware(['auth'])->group(function () {
    Route::get('/fiyat-listesi', [LisansController::class, 'showPriceList'])->name('priceList');
    Route::get('/fiyatelistesi', [LisansController::class, 'showPriceList']);
    Route::post('/fiyat-listesi/{id}', [LisansController::class, 'updateListPrice'])->name('priceList.update');

    Route::get('/Agreement', [SozlesmelerController::class, 'Agreement'])->name('Agreement');
    Route::get('/DownloadAgreement/{id}', [SozlesmelerController::class, 'DownloadAgreement'])->name('DownloadAgreement');
    Route::post('/AddAgreement', [SozlesmelerController::class, 'AddAgreement'])->name('AddAgreement');
    Route::get('/LisansSozlesme/{siparisNo}/{tip}', [SozlesmelerController::class, 'LicenseAgreement'])->name('LicenseAgreement');

    //Bayi İşlemleri
    Route::get('/Dealer', [BayiController::class, 'Dealer'])->name('Dealer');
    Route::post('/AddDealer', [BayiController::class, 'AddDealer'])->name('AddDealer');
    Route::get('/DeleteDealer/{id}', [BayiController::class, 'DeleteDealer'])->name('DeleteDealer');
    Route::get('/EditDealer/{id}', [BayiController::class, 'EditDealer'])->name('EditDealer');
    Route::post('/UpdateDealer/{id}', [BayiController::class, 'UpdateDealer'])->name('UpdateDealer');
    Route::post('/DealerBalance/{id}', [BayiController::class, 'DealerBalance'])->name('DealerBalance');
    Route::post('/dealer/update-prices/{id}', [App\Http\Controllers\BayiController::class, 'UpdateDealerPrices'])->name('UpdateDealerPrices');


    Route::get('/License/{id}', [LisansController::class, 'License'])->name('License');
    Route::get('/CustomerLicense/{id}', [LisansController::class, 'CustomerLicense'])->name('CustomerLicense');
    Route::post('/AddLicense', [LisansController::class, 'AddLicense'])->name('AddLicense');
    Route::post('/lisans/update-with-siparis/{siparisNo}', [LisansController::class, 'updateWithSiparis'])->name('updateWithSiparis');
    Route::get('/LisanceDown/{SiparisNo}', [MusteriController::class, 'LisanceDown'])->name('LisanceDown');
    Route::get('/LisanceUp/{SiparisNo}', [MusteriController::class, 'LisanceUp'])->name('LisanceUp');

    Route::get('/AllLicenses', [LisansController::class, 'AllLicenses'])->name('AllLicenses');
    Route::get('/UpcomingLicenses', [LisansController::class, 'UpcomingLicenses'])->name('UpcomingLicenses');
    Route::post('/extend-license', [LisansController::class, 'ExtendLicense'])->name('extend.license');

    Route::get('/Customer', [MusteriController::class, 'Customer'])->name('Customer');
    Route::post('/AddCustomer', [MusteriController::class, 'AddCustomer'])->name('AddCustomer');
    Route::post('/UpdateCustomer/{id}', [MusteriController::class, 'UpdateCustomer'])->name('UpdateCustomer');
    Route::get('/DeleteCustomer/{id}', [MusteriController::class, 'DeleteCustomer'])->name('DeleteCustomer');
    Route::get('/EditCustomer/{id}', [MusteriController::class, 'EditCustomer'])->name('EditCustomer');
    Route::get('/V1Customer', [MusteriController::class, 'V1Customer'])->name('V1Customer');
    Route::get('/V1Change/{id}', [MusteriController::class, 'V1Change'])->name('V1Change');

    Route::get('/Log', [LogController::class, 'Log'])->name('Log');
    Route::get('/Comingsoon', [AyarlarController::class, 'Comingsoon'])->name('Comingsoon');
    Route::get('/MicroService', [AyarlarController::class, 'MicroService'])->name('MicroService');
    Route::get('/MicroService/ping', [App\Http\Controllers\MicroServicePingController::class, 'ping'])->name('MicroServicePing');
    Route::get('/PayTR', [AyarlarController::class, 'PayTR'])->name('PayTR');
    Route::get('/SMS', [AyarlarController::class, 'SMS'])->name('SMS');
    Route::post('/UpdateSMS', [AyarlarController::class, 'UpdateSMS'])->name('UpdateSMS');
    Route::post('/UpdatePayTR', [AyarlarController::class, 'UpdatePayTR'])->name('UpdatePayTR');

    Route::get('/File', [DosyaSistemi::class, 'File'])->name('File');
    Route::post('/AddFile', [DosyaSistemi::class, 'AddFile'])->name('AddFile');
    Route::get('/DeleteFile/{id}', [DosyaSistemi::class, 'DeleteFile'])->name('DeleteFile');
    Route::get('/Certificate', [AyarlarController::class, 'Certificate'])->name('Certificate');
    
    //Teklif  İşlemleri
    Route::get('/Offer', [TeklifController::class, 'Offer'])->name('Offer');
    Route::post('/AddOffer', [TeklifController::class, 'AddOffer'])->name('AddOffer');
    Route::get('/OfferList', [TeklifController::class, 'OfferList'])->name('OfferList');
    Route::get('/teklifPdfIndir/{teklif_no}', [TeklifController::class, 'teklifPdfIndir'])->name('teklifPdfIndir');
    Route::get('/Offer/{id}/edit', [TeklifController::class, 'edit'])->name('EditOffer');
    Route::put('/Offer/{id}', [TeklifController::class, 'update'])->name('UpdateOffer');
    Route::get('/OfferDelete/{id}', [TeklifController::class, 'destroy'])->name('OfferDelete');

    Route::get('/api/lisans-detay/{siparisNo}', [App\Http\Controllers\LisansController::class, 'getLicenseDetailsByOrder'])->name('api.lisans.detay');
    
    // Patron İşlemleri
    Route::post('/patron/add',         [PatronController::class, 'add'])->name('patron.add');
    Route::post('/patron/{id}/update', [PatronController::class, 'update'])->name('patron.update');
    Route::post('/patron/{id}/delete', [PatronController::class, 'delete'])->name('patron.delete');

	// ─────── YEDEK İŞLEMLERİ ───────
    Route::get('/yedek/{id}/download', [\App\Http\Controllers\Api\YedekController::class, 'download'])->name('yedek.download');
    Route::delete('/yedek/{id}', [\App\Http\Controllers\Api\YedekController::class, 'deleteById'])->name('yedek.delete');


    // Güncellemeler — BURAYA EKLEYİN
    Route::prefix('guncelleme')->name('guncelleme.')->group(function () {
        Route::get('/',             [GuncelleController::class, 'index'])        ->name('index');
        Route::get('/olustur',      [GuncelleController::class, 'create'])       ->name('create');
        Route::post('/',            [GuncelleController::class, 'store'])        ->name('store');
        Route::get('/{id}',         [GuncelleController::class, 'show'])         ->name('show');
        Route::post('/{id}/toggle', [GuncelleController::class, 'toggleDurum']) ->name('toggle');
        Route::delete('/{id}',      [GuncelleController::class, 'destroy'])      ->name('destroy');
        Route::get('/{id}/indir',   [GuncelleController::class, 'downloadSetup'])->name('download');
    });


    });


Route::middleware(['auth', 'role:admin'])->group(function () {

    //Admin İşlemleri
    Route::get('/Desk', [AdminController::class, 'dashboard'])->name('Desk');

    Route::get('/admin/yazarkasa-raporu', [App\Http\Controllers\LisansController::class, 'YazarKasaReport'])->name('admin.yazarkasa_report')->middleware('auth');

    //Bayi İşlemleri
    // Route::get('/Dealer', [BayiController::class, 'Dealer'])->name('Dealer');
    // Route::post('/AddDealer', [BayiController::class, 'AddDealer'])->name('AddDealer');
    // Route::get('/DeleteDealer/{id}', [BayiController::class, 'DeleteDealer'])->name('DeleteDealer');
    // Route::get('/EditDealer/{id}', [BayiController::class, 'EditDealer'])->name('EditDealer');
    // Route::post('/UpdateDealer/{id}', [BayiController::class, 'UpdateDealer'])->name('UpdateDealer');
    // Route::post('/DealerBalance/{id}', [BayiController::class, 'DealerBalance'])->name('DealerBalance');


    //Müşteri İşlemleri
    // Route::get('/Customer', [MusteriController::class, 'Customer'])->name('Customer');
    // Route::post('/AddCustomer', [MusteriController::class, 'AddCustomer'])->name('AddCustomer');
    // Route::post('/UpdateCustomer/{id}', [MusteriController::class, 'UpdateCustomer'])->name('UpdateCustomer');
    // Route::get('/DeleteCustomer/{id}', [MusteriController::class, 'DeleteCustomer'])->name('DeleteCustomer');
    // Route::get('/EditCustomer/{id}', [MusteriController::class, 'EditCustomer'])->name('EditCustomer');
    // Route::get('/V1Customer', [MusteriController::class, 'V1Customer'])->name('V1Customer');
    // Route::get('/V1Change/{id}', [MusteriController::class, 'V1Change'])->name('V1Change');


    //Lisans İşlemleri
    // Route::get('/License/{id}', [LisansController::class, 'License'])->name('License');
    // Route::get('/CustomerLicense/{id}', [LisansController::class, 'CustomerLicense'])->name('CustomerLicense');
    // Route::post('/AddLicense', [LisansController::class, 'AddLicense'])->name('AddLicense');
    // Route::post('/lisans/update-with-siparis/{siparisNo}', [MusteriController::class, 'updateWithSiparis'])->name('lisans.updateWithSiparis');
    // Route::get('/LisanceDown/{SiparisNo}', [MusteriController::class, 'LisanceDown'])->name('LisanceDown');
    // Route::get('/LisanceUp/{SiparisNo}', [MusteriController::class, 'LisanceUp'])->name('LisanceUp');


    // Route::get('/AllLicenses', [LisansController::class, 'AllLicenses'])->name('AllLicenses');
    // Route::get('/UpcomingLicenses', [LisansController::class, 'UpcomingLicenses'])->name('UpcomingLicenses');
    // Route::post('/extend-license', [LisansController::class, 'ExtendLicense'])->name('extend.license');

    //Sözleşmeler 
    // Route::get('/Agreement', [SozlesmelerController::class, 'Agreement'])->name('Agreement');
    // Route::get('/DownloadAgreement/{id}', [SozlesmelerController::class, 'DownloadAgreement'])->name('DownloadAgreement');
    // Route::post('/AddAgreement', [SozlesmelerController::class, 'AddAgreement'])->name('AddAgreement');

    //Bildirim Sistemi
    Route::get('/CustomerSMS', [BildirimlerController::class, 'CustomerSMS'])->name('CustomerSMS');
    Route::get('/DealerSMS', [BildirimlerController::class, 'DealerSMS'])->name('DealerSMS');
    Route::get('/SoftwaresMessage', [BildirimlerController::class, 'SoftwaresMessage'])->name('SoftwaresMessage');
    Route::get('/PanelMessage', [BildirimlerController::class, 'PanelMessage'])->name('PanelMessage');
    Route::get('/LoginMessage', [BildirimlerController::class, 'LoginMessage'])->name('LoginMessage');
    Route::post('/AddLoginMessage', [BildirimlerController::class, 'AddLoginMessage'])->name('AddLoginMessage');
    Route::post('/EditLoginMessage/{id}', [BildirimlerController::class, 'EditLoginMessage'])->name('EditLoginMessage');
    Route::get('/DeleteLoginMessage/{id}', [BildirimlerController::class, 'DeleteLoginMessage'])->name('DeleteLoginMessage');
    Route::get('/bildirim/softwares-message', [BildirimlerController::class, 'SoftwaresMessage'])->name('softwaresMessage');
    Route::post('/bildirim/softwares-message-send', [BildirimlerController::class, 'SoftwaresMessageSend'])->name('softwaresMessageSend');

    //Program Paketleri 
    Route::get('/ProgramPackages', [ProgramPaketleriController::class, 'ProgramPackages'])->name('ProgramPackages');
    Route::post('/AddProgramPackages', [ProgramPaketleriController::class, 'AddProgramPackages'])->name('AddProgramPackages');
    Route::post('/UpdateProgramPackages/{id}', [ProgramPaketleriController::class, 'UpdateProgramPackages'])->name('UpdateProgramPackages');

    //Log İşlemleri
    // Route::get('/Log', [LogController::class, 'Log'])->name('Log');
    // Route::get('/Comingsoon', [AyarlarController::class, 'Comingsoon'])->name('Comingsoon');
    // Route::get('/MicroService', [AyarlarController::class, 'MicroService'])->name('MicroService');
    // Route::get('/PayTR', [AyarlarController::class, 'PayTR'])->name('PayTR');
    // Route::get('/SMS', [AyarlarController::class, 'SMS'])->name('SMS');
    // Route::post('/UpdateSMS', [AyarlarController::class, 'UpdateSMS'])->name('UpdateSMS');
    // Route::post('/UpdatePayTR', [AyarlarController::class, 'UpdatePayTR'])->name('UpdatePayTR'); 
 
    //Dosya Sistemi
    // Route::get('/File', [DosyaSistemi::class, 'File'])->name('File');
    // Route::post('/AddFile', [DosyaSistemi::class, 'AddFile'])->name('AddFile');
    // Route::get('/DeleteFile/{id}', [DosyaSistemi::class, 'DeleteFile'])->name('DeleteFile');
    // Route::get('/Certificate', [AyarlarController::class, 'Certificate'])->name('Certificate');

    // Ödeme

    Route::get('/Cash', [PayTRController::class, 'showPaymentForm'])->name('Cash');
    Route::get('/basarili', [PayTRController::class, 'successPayment'])->name('payment.success');
    Route::get('/basarisiz', [PayTRController::class, 'failedPayment'])->name('payment.failed');


});

Route::middleware(['auth', 'role:agent'])->group(function () {
    Route::get('/agent/dashboard', [AgentController::class, 'dashboard'])->name('agent.dashboard');

    //Müşteri İşlemleri
    Route::get('/agent/Customer', [MusteriController::class, 'agentCustomer'])->name('agentCustomer');
    Route::post('/agent/AddCustomer', [MusteriController::class, 'agentAddCustomer'])->name('agentAddCustomer');
    Route::get('/agent/DeleteCustomer/{id}', [MusteriController::class, 'agentDeleteCustomer'])->name('agentDeleteCustomer');
    Route::get('/agent/EditCustomer/{id}', [MusteriController::class, 'agentEditCustomer'])->name('agentEditCustomer');

    //Log İşlemleri
    Route::get('/agent/Log', [LogController::class, 'agentLog'])->name('agentLog');

    //Lisans İşlemleri
    Route::get('/agent/License/{id}', [LisansController::class, 'agentLicense'])->name('agentLicense');
    Route::post('/agent/AddLicense', [LisansController::class, 'agentAddLicense'])->name('agentAddLicense');
    Route::post('/agent/lisans/update-with-siparis/{siparisNo}', [LisansController::class, 'updateWithSiparis'])->name('lisans.updateWithSiparis');
    Route::get('/agent/LisanceDown/{SiparisNo}', [MusteriController::class, 'agentLisanceDown'])->name('agentLisanceDown');
    Route::get('/agent/LisanceUp/{SiparisNo}', [MusteriController::class, 'agentLisanceUp'])->name('agentLisanceUp');
});

// ═══════════════════════════════════════════════════════════════
//   SYNC EXPORT ROUTES (POS → Panel'den veri çekiyor)
// ═══════════════════════════════════════════════════════════════


Route::prefix('api/sync')->withoutMiddleware(['web', 'auth'])->group(function () {
    Route::get('/products',  [SyncExportController::class, 'products']);
    Route::get('/receipts',  [SyncExportController::class, 'receipts']);
    Route::get('/customers', [SyncExportController::class, 'customers']);
    Route::get('/tables',    [SyncExportController::class, 'tables']);
    Route::get('/invoices',  [SyncExportController::class, 'invoices']);
    Route::get('/all',       [SyncExportController::class, 'all']);
});

require __DIR__ . '/auth.php';
