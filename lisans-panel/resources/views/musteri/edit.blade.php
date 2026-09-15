@extends('layouts.app')

@section('content')
<style>
    /* --- MODERN UI TASARIMI --- */
    :root {
        --primary-color: #4f46e5;
        --primary-hover: #4338ca;
        --secondary-color: #64748b;
        --border-color: #e2e8f0;
        --bg-light: #f8fafc;
    }

    /* Genel Layout */
    .main-content {
        font-family: 'Inter', system-ui, -apple-system, sans-serif;
        color: #334155;
    }

    /* İstatistik Kartları */
    .stat-card {
        background: #fff;
        border: 1px solid var(--border-color);
        border-radius: 12px;
        padding: 1.5rem;
        transition: all 0.2s ease;
    }
    .stat-card:hover {
        border-color: #cbd5e1;
        box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);
    }
    .stat-card .icon-wrapper {
        width: 42px;
        height: 42px;
        border-radius: 10px;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 1.25rem;
        margin-bottom: 0.75rem;
    }

    /* Modern Tablo */
    .table-modern {
        width: 100%;
        border-collapse: separate;
        border-spacing: 0;
    }
    .table-modern th {
        background: #f8fafc;
        font-weight: 600;
        text-transform: uppercase;
        font-size: 0.7rem;
        letter-spacing: 0.05em;
        color: #64748b;
        padding: 1rem 1.5rem;
        border-bottom: 1px solid var(--border-color);
    }
    .table-modern td {
        padding: 1rem 1.5rem;
        border-bottom: 1px solid var(--border-color);
        vertical-align: middle;
        font-size: 0.875rem;
    }
    .table-modern tr:last-child td {
        border-bottom: none;
    }
    .table-modern tbody tr:hover {
        background-color: #fcfcfc;
    }

    /* Modal & Form Elemanları */
    .form-control, .form-select {
        border-color: #e2e8f0;
        border-radius: 0.5rem;
        padding: 0.6rem 1rem;
        font-size: 0.875rem;
    }
    .form-control:focus {
        border-color: var(--primary-color);
        box-shadow: 0 0 0 3px rgba(79, 70, 229, 0.1);
    }
    /* Kurumsal Modal Tasarımı */
    .corporate-modal .modal-content {
        border: none;
        border-radius: 16px;
        box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
        overflow: hidden;
    }
    .corporate-modal .modal-header {
        border-bottom: 1px solid #f1f5f9;
        padding: 1.5rem;
        background: #fff;
    }
    .corporate-modal .modal-footer {
        border-top: 1px solid #f1f5f9;
        padding: 1.5rem;
        background: #f8fafc;
    }


    .saha-pills .nav-link {
        color: #64748b;
        font-weight: 600;
        font-size: .82rem;
        padding: .4rem .85rem;
    }
    .saha-pills .nav-link.active {
        background: #1e293b;
        color: #fff;
    }

    /* Tablar */
    .nav-tabs .nav-link {
        border: none;
        color: #64748b;
        font-weight: 500;
        padding: 1rem 1.5rem;
        border-bottom: 2px solid transparent;
    }
    .nav-tabs .nav-link.active {
        color: var(--primary-color);
        border-bottom-color: var(--primary-color);
        background: transparent;
    }
    .nav-tabs .nav-link:hover:not(.active) {
        color: #334155;
        border-bottom-color: #cbd5e1;
    }

    /* Custom Toast (Bildirim) */
    .toast-container-custom {
        position: fixed;
        top: 24px;
        right: 24px;
        z-index: 9999;
        display: flex;
        flex-direction: column;
        gap: 12px;
    }
    .custom-toast {
        background: white;
        border-radius: 12px;
        box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05);
        padding: 1rem;
        border-left: 5px solid var(--primary-color);
        display: flex;
        align-items: center;
        min-width: 320px;
        max-width: 400px;
        animation: slideInRight 0.4s cubic-bezier(0.16, 1, 0.3, 1);
        backdrop-filter: blur(10px);
    }
    .custom-toast.success { border-left-color: #10b981; }
    .custom-toast.error { border-left-color: #ef4444; }
    .custom-toast.warning { border-left-color: #f59e0b; }
    
    @keyframes slideInRight {
        from { transform: translateX(100%); opacity: 0; }
        to { transform: translateX(0); opacity: 1; }
    }
    @keyframes fadeOutRight {
        to { opacity: 0; transform: translateX(100%); }
    }

    .yanipsonenyazi { animation: blinker 2s linear infinite; color: #0ea5e9; font-weight: 700; }
    @keyframes blinker { 50% { opacity: 0.5; } }
</style>

<div class="main-content pt-4 px-4">

    {{-- Başlık ve Geri Dön Butonu --}}
    <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center mb-5">
        <div>
            <h3 class="fw-bold text-dark mb-1">{{ $Musteri->Unvan }}</h3>
            <div class="d-flex align-items-center text-muted fs-7">
                <i class="material-icons-outlined fs-6 me-1">store</i> {{ $Musteri->TabelaAdi }}
                <span class="mx-2 text-gray-300">|</span>
                <i class="material-icons-outlined fs-6 me-1">pin_drop</i> {{ $Musteri->Il }} / {{ $Musteri->Ilce }}
            </div>
        </div>
        <div class="mt-3 mt-md-0">
            <a href="{{ route(Auth::user()->role == 'admin' ? 'Customer' : 'agentCustomer') }}" class="btn btn-white border shadow-sm fw-medium px-4">
                <i class="material-icons-outlined fs-5 align-middle me-1">arrow_back</i> Listeye Dön
            </a>
        </div>
    </div>

    {{-- Ana Tablo Alanı --}}
    <div class="card border-0 shadow-sm rounded-4 overflow-hidden mb-5">
        <div class="card-header bg-white border-bottom-0 p-0">
            <ul class="nav nav-tabs px-4 pt-2" role="tablist">
                <li class="nav-item">
                    <a class="nav-link active" data-bs-toggle="tab" href="#genelbilgiler" role="tab">
                        <i class="bi bi-building me-2"></i>Genel Bilgiler
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" data-bs-toggle="tab" href="#lisanslar" role="tab">
                        <i class="bi bi-key me-2"></i>Lisans Yönetimi
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" data-bs-toggle="tab" href="#saha" role="tab">
                        <i class="bi bi-shop me-2"></i>Saha
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" data-bs-toggle="tab" href="#sync-yedekler" role="tab">
                        <i class="bi bi-cloud-arrow-down me-2"></i>Yedekler
                        <span class="badge bg-danger ms-1">{{ $syncYedekler->count() }}</span>
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" data-bs-toggle="tab" href="#sozlesmeler" role="tab">
                        <i class="bi bi-file-earmark-text me-2"></i>Sözleşmeler
                    </a>
                </li>
            </ul>
        </div>

        <div class="card-body p-0">
            <div class="tab-content">
                
                {{-- TAB 1: Genel Bilgiler --}}
                <div class="tab-pane fade show active p-5" id="genelbilgiler" role="tabpanel">
                    <form method="POST" action="{{ route('UpdateCustomer', $Musteri->id) }}">
                        @csrf
                        <div class="row g-5">
                            <div class="col-lg-6 border-end-lg">
                                <h6 class="text-uppercase text-muted fw-bold fs-8 mb-4 ls-1">Kurumsal Kimlik</h6>
                                @if (Auth::user()->role == 'admin')
                                <div class="form-floating mb-3">
                                    <select class="form-select" id="Bayi" name="Bayi">
                                        <option selected value="{{ $Musteri->Bayi }}">{{ optional($Musteri->kimbubayi)->Unvan ?? 'Bilinmiyor' }}</option>
                                        @foreach ($Bayi as $GeldiBayi)
                                            <option value="{{ $GeldiBayi->id }}">{{ $GeldiBayi->name }}</option>
                                        @endforeach
                                    </select>
                                    <label for="Bayi">Bağlı Bayi</label>
                                </div>
                                @endif

                                <div class="form-floating mb-3">
                                    <input type="text" class="form-control" id="Unvan" name="Unvan" value="{{ $Musteri->Unvan }}" required>
                                    <label for="Unvan">Ticari Ünvan</label>
                                </div>

                                <div class="form-floating mb-3">
                                    <input type="text" class="form-control" id="TabelaAdi" name="TabelaAdi" value="{{ $Musteri->TabelaAdi }}" required>
                                    <label for="TabelaAdi">Tabela Adı</label>
                                </div>

                                <div class="row g-2">
                                    <div class="col-md-6">
                                        <div class="form-floating">
                                            <input type="text" class="form-control" id="VergiNo" name="VergiNo" value="{{ $Musteri->VergiNo }}" required pattern="\d{10}" maxlength="10">
                                            <label for="VergiNo">Vergi No</label>
                                        </div>
                                    </div>
                                    <div class="col-md-6">
                                        <div class="form-floating">
                                            <input type="text" class="form-control" id="VergiDairesi" name="VergiDairesi" value="{{ $Musteri->VergiDairesi }}" required>
                                            <label for="VergiDairesi">Vergi Dairesi</label>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div class="col-lg-6">
                                <h6 class="text-uppercase text-muted fw-bold fs-8 mb-4 ls-1">İletişim & Lokasyon</h6>
                                <div class="row g-2 mb-3">
                                    <div class="col-md-6">
                                        <div class="form-floating">
                                            <input type="text" class="form-control" id="Yetkili" name="Yetkili" value="{{ $Musteri->Yetkili }}" required>
                                            <label for="Yetkili">Yetkili Ad Soyad</label>
                                        </div>
                                    </div>
                                    <div class="col-md-6">
                                        <div class="form-floating">
                                            <input type="email" class="form-control" id="Email" name="Email" value="{{ $Musteri->EMail }}" required>
                                            <label for="Email">E-Posta</label>
                                        </div>
                                    </div>
                                </div>

                                <div class="row g-2 mb-3">
                                    <div class="col-md-6">
                                        <div class="form-floating">
                                            <input type="text" class="form-control" id="YetkiliGsm" name="YetkiliGsm" value="{{ $Musteri->YetkiliGsm }}" required>
                                            <label for="YetkiliGsm">GSM</label>
                                        </div>
                                    </div>
                                    <div class="col-md-6">
                                        <div class="form-floating">
                                            <input type="text" class="form-control" id="Telefon" name="Telefon" value="{{ $Musteri->Telefon }}" required>
                                            <label for="Telefon">Sabit Telefon</label>
                                        </div>
                                    </div>
                                </div>

                                <div class="row g-2 mb-3">
                                    <div class="col-md-4">
                                        <div class="form-floating">
                                            <select class="form-select" id="Ulke" name="Ulke">
                                                <option selected value="{{ $Musteri->Ulke }}">{{ $Musteri->Ulke }}</option>
                                                <option value="Türkiye">Türkiye</option>
                                            </select>
                                            <label for="Ulke">Ülke</label>
                                        </div>
                                    </div>
                                    <div class="col-md-4">
                                        <div class="form-floating">
                                            <select class="form-select" id="Iller" name="Il">
                                                <option selected value="{{ $Musteri->Il }}">{{ $Musteri->Il }}</option>
                                            </select>
                                            <label for="Iller">İl</label>
                                        </div>
                                    </div>
                                    <div class="col-md-4">
                                        <div class="form-floating">
                                            <select class="form-select" id="Ilceler" name="Ilce" disabled>
                                                <option selected value="{{ $Musteri->Ilce }}">{{ $Musteri->Ilce }}</option>
                                            </select>
                                            <label for="Ilceler">İlçe</label>
                                        </div>
                                    </div>
                                </div>

                                <div class="form-floating">
                                    <textarea class="form-control" id="Adres" name="Adres" style="height: 100px" required>{{ $Musteri->Adres }}</textarea>
                                    <label for="Adres">Açık Adres</label>
                                </div>
                            </div>
                        </div>
                        <div class="d-flex justify-content-end mt-4 pt-3 border-top">
                            <button type="submit" class="btn btn-primary btn-lg px-5 shadow-sm">
                                <i class="bi bi-check2-circle me-2"></i>Güncelle
                            </button>
                        </div>
                    </form>
                </div>

                {{-- TAB 2: Lisans Yönetimi --}}
                <div class="tab-pane fade" id="lisanslar" role="tabpanel">
                    <div class="table-responsive">
                        <table class="table table-modern w-100 mb-0">
                            <thead>
                                <tr>
                                    <th>Sipariş No</th>
                                    <th>PC Adı</th>
                                    <th>Durum</th>
                                    <th>Lisans Anahtarı</th>
                                    <th>Karşılık Anahtarı</th>
                                    <th class="text-end">İşlemler</th>
                                </tr>
                            </thead>
                            <tbody>
                                @foreach ($Lisanslar as $GeldiLisans)
                                    @if ($GeldiLisans->Tipi == 'satis')
                                        <tr>
                                            <td class="font-monospace fw-bold">{{ $GeldiLisans->SiparisNo }}</td>
                                            <td>
                                                <div class="d-flex align-items-center">
                                                    <span class="material-icons-outlined text-muted me-2 fs-6">computer</span>
                                                    {{ $GeldiLisans->PcName }}
                                                </div>
                                            </td>
                                            <td>
                                                @if ($GeldiLisans->Durum == '1')
                                                    <span class="badge bg-success bg-opacity-10 text-success rounded-pill px-3">Aktif</span>
                                                @else
                                                    <span class="badge bg-danger bg-opacity-10 text-danger rounded-pill px-3">Pasif</span>
                                                @endif
                                            </td>
                                            <td>
                                                <div class="d-flex align-items-center bg-light rounded px-2 py-1 border border-light" style="width: fit-content;">
                                                    <span class="font-monospace fs-7 me-2">{{ $GeldiLisans->Anahtar }}</span>
                                                    <button class="btn btn-sm p-0 text-muted" onclick="copyToClipboard('{{ $GeldiLisans->Anahtar }}')" title="Kopyala">
                                                        <i class="bi bi-clipboard"></i>
                                                    </button>
                                                </div>
                                            </td>
                                            <td>
                                                @if ($GeldiLisans->AnahtarKarsilik == '' && $GeldiLisans->Durum == '1')
                                                    <span class="text-primary fw-bold yanipsonenyazi fs-7">
                                                        <i class="bi bi-hourglass-split me-1"></i>Bekleniyor...
                                                    </span>
                                                @else
                                                    <span class="font-monospace text-dark">{{ $GeldiLisans->AnahtarKarsilik ?: '-' }}</span>
                                                @endif
                                            </td>
                                            <td class="text-end">
                                                <div class="d-flex align-items-center justify-content-end gap-2">
                                                    @if ($GeldiLisans->Durum == '1')
                                                        {{-- Güncelle Butonu --}}
                                                        <button class="btn btn-white border btn-sm px-3 shadow-sm d-flex align-items-center" 
                                                                data-bs-toggle="modal" 
                                                                data-bs-target="#detailModal-{{ $GeldiLisans->SiparisNo }}">
                                                            <i class="material-icons-outlined fs-6 me-1">edit</i> Güncelle
                                                        </button>

                                                        @if (Auth::user()->role == 'admin' || Auth::user()->role == 'agent')
                                                            <button type="button" class="btn btn-outline-danger btn-sm px-2"
                                                                    onclick="confirmAction('{{ Auth::user()->role == 'admin' ? route('LisanceDown', $GeldiLisans->SiparisNo) : route('agentLisanceDown', $GeldiLisans->SiparisNo) }}', 'Bu lisansı pasife almak istediğinize emin misiniz?')">
                                                                <i class="material-icons-outlined fs-6">block</i>
                                                            </button>
                                                        @endif
                                                    @else
                                                        @if (Auth::user()->role == 'admin' || Auth::user()->role == 'agent')
                                                            <a href="{{ Auth::user()->role == 'admin' ? route('LisanceUp', $GeldiLisans->SiparisNo) : route('agentLisanceUp', $GeldiLisans->SiparisNo) }}" 
                                                               class="btn btn-success btn-sm px-3 shadow-sm text-white d-flex align-items-center">
                                                                <i class="material-icons-outlined fs-6 me-1">check_circle</i> Aktifleştir
                                                            </a>
                                                        @endif
                                                    @endif
                                                </div>
                                            </td>
                                        </tr>
                                    @endif
                                @endforeach
                            </tbody>
                        </table>
                    </div>

                    {{-- LİSANS DETAY MODALLARI --}}
                    @foreach ($Lisanslar as $GeldiLisans)
                        @if ($GeldiLisans->Tipi == 'satis')
                            <div class="modal fade corporate-modal" id="detailModal-{{ $GeldiLisans->SiparisNo }}" tabindex="-1" aria-hidden="true">
                                <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
                                    <div class="modal-content">
                                        <div class="modal-header">
                                            <div>
                                                <h5 class="modal-title fw-bold text-dark">Paket & Modül Yönetimi</h5>
                                                <div class="font-monospace text-muted fs-7 mt-1">{{ $GeldiLisans->SiparisNo }}</div>
                                            </div>
                                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Kapat"></button>
                                        </div>
                                        
                                        <div class="modal-body p-0 bg-white">
                                            @php
                                                // JSON Decode
                                                $programlar = is_string($GeldiLisans->Lisans) ? json_decode($GeldiLisans->Lisans, true) : [];
                                                $programlar = is_array($programlar) ? $programlar : [];
                                                
                                                // Paketleri Grupla
                                                $groupedPaketler = [];
                                                foreach ($LisansPaket as $paket) {
                                                    if (!isset($groupedPaketler[$paket->PaketTipi])) {
                                                        $groupedPaketler[$paket->PaketTipi] = [];
                                                    }
                                                    $groupedPaketler[$paket->PaketTipi][] = $paket;
                                                }
                                                ksort($groupedPaketler);
                                                // 'paket' tipini en başa al
                                                if (isset($groupedPaketler['paket'])) {
                                                    $temp = $groupedPaketler['paket'];
                                                    unset($groupedPaketler['paket']);
                                                    $groupedPaketler = ['paket' => $temp] + $groupedPaketler;
                                                }
                                            @endphp

                                            <div class="alert alert-light border m-4 d-flex align-items-center">
                                                <i class="bi bi-info-circle-fill text-primary fs-4 me-3"></i>
                                                <div>
                                                    <strong class="d-block text-dark">Bilgilendirme</strong>
                                                    <span class="fs-7 text-muted">Yapılan değişiklikler kaydedildiğinde fark bakiyenize yansıtılır. Seri numarası girmek için yazarkasa paketlerini aktifleştirin.</span>
                                                </div>
                                            </div>

                                            <div class="px-4 mb-3">
                                                <label class="form-label text-muted fw-semibold mb-1" style="font-size: .75rem;">PC Adı</label>
                                                <div class="input-group input-group-sm">
                                                    <span class="input-group-text bg-white text-muted">
                                                        <span class="material-icons-outlined" style="font-size: 16px;">computer</span>
                                                    </span>
                                                    <input type="text"
                                                           class="form-control pc-name-input"
                                                           value="{{ $GeldiLisans->PcName }}"
                                                           maxlength="48"
                                                           autocomplete="off"
                                                           placeholder="Örn: Kasa-1">
                                                </div>
                                            </div>

                                            <div class="accordion accordion-flush px-3 pb-4" id="accordion{{ $GeldiLisans->SiparisNo }}">
                                                @foreach ($groupedPaketler as $paketTipi => $paketler)
                                                    <div class="accordion-item border rounded-3 mb-2 overflow-hidden">
                                                        <h2 class="accordion-header">
                                                            <button class="accordion-button collapsed fw-bold bg-light text-dark" type="button" data-bs-toggle="collapse" data-bs-target="#collapse{{ $paketTipi }}{{ $GeldiLisans->SiparisNo }}">
                                                                {{ $paketTipi == 'paket' ? 'Ana Yazılım Paketleri' : ($paketTipi == 'yazarkasa' ? 'Yazar Kasa Entegrasyonları' : ucfirst($paketTipi)) }}
                                                            </button>
                                                        </h2>
                                                        <div id="collapse{{ $paketTipi }}{{ $GeldiLisans->SiparisNo }}" class="accordion-collapse collapse {{ $loop->first ? 'show' : '' }}" data-bs-parent="#accordion{{ $GeldiLisans->SiparisNo }}">
                                                            <div class="accordion-body p-0">
                                                                <table class="table table-hover mb-0 fs-7">
                                                                    <thead class="bg-white border-bottom">
                                                                        <tr>
                                                                            <th class="ps-4 text-muted fw-semibold" style="width: 40%">Paket Adı</th>
                                                                            <th class="text-center text-muted fw-semibold" style="width: 15%">Durum</th>
                                                                            <th class="text-center text-muted fw-semibold" style="width: 20%">Adet</th>
                                                                            <th class="pe-4 text-muted fw-semibold" style="width: 25%">Bitiş</th>
                                                                        </tr>
                                                                    </thead>
                                                                    <tbody>
                                                                        @foreach ($paketler as $paket)
                                                                            @php
                                                                                $isChecked = false;
                                                                                $programDate = '';
                                                                                $adet = 0;
                                                                                $existingSerials = [];

                                                                                // MEVCUT JSON VERİSİNDEN DURUM KONTROLÜ
                                                                                foreach ($programlar as $p) {
                                                                                    // 1. Yazarkasa Array Kontrolü
                                                                                    if (isset($p['yazarkasa']) && is_array($p['yazarkasa'])) {
                                                                                        foreach ($p['yazarkasa'] as $yazarkasaPaket) {
                                                                                            if (isset($yazarkasaPaket['paketName']) && $yazarkasaPaket['paketName'] == $paket->PaketName && isset($yazarkasaPaket['status']) && $yazarkasaPaket['status'] == 1) {
                                                                                                $isChecked = true;
                                                                                                $adet = isset($yazarkasaPaket['adet']) ? $yazarkasaPaket['adet'] : 1;
                                                                                                $existingSerials = isset($yazarkasaPaket['serial_numbers']) ? $yazarkasaPaket['serial_numbers'] : [];
                                                                                                if (isset($yazarkasaPaket['date']) && $yazarkasaPaket['date'] != 'N/A') {
                                                                                                    try { $programDate = date('Y-m-d', strtotime($yazarkasaPaket['date'])); } catch (Exception $e) {}
                                                                                                }
                                                                                                break;
                                                                                            }
                                                                                        }
                                                                                    } 
                                                                                    // 2. Düz Paket Kontrolü
                                                                                    elseif (isset($p['paketName']) && $p['paketName'] == $paket->PaketName && isset($p['status']) && $p['status'] == 1) {
                                                                                        $isChecked = true;
                                                                                        $adet = isset($p['adet']) ? $p['adet'] : 1;
                                                                                        // Düz paketlerde seri no varsa al (nadiren olur)
                                                                                        $existingSerials = isset($p['serial_numbers']) ? $p['serial_numbers'] : [];
                                                                                        if (isset($p['date']) && $p['date'] != 'N/A') {
                                                                                            try { $programDate = date('Y-m-d', strtotime($p['date'])); } catch (Exception $e) {}
                                                                                        }
                                                                                        break;
                                                                                    }
                                                                                }
                                                                            @endphp
                                                                            
                                                                            <tr>
                                                                                <td class="ps-4 align-middle fw-medium">
                                                                                    {{ $paket->PaketAdi }}
                                                                                    @if($paket->PaketTipi == 'yazarkasa')
                                                                                        <i class="bi bi-receipt text-muted ms-1" title="Yazarkasa"></i>
                                                                                    @endif
                                                                                </td>
                                                                                <td class="align-middle text-center">
                                                                                    <div class="form-check form-switch d-inline-block">
                                                                                        <input class="form-check-input update-checkbox" type="checkbox" role="switch"
                                                                                            name="paketler[]" 
                                                                                            value="{{ $paket->PaketName }}"
                                                                                            data-paket-tipi="{{ $paket->PaketTipi }}" 
                                                                                            {{ $isChecked ? 'checked' : '' }}
                                                                                            @if($paket->PaketTipi == 'yazarkasa')
                                                                                                data-bs-toggle="collapse" 
                                                                                                data-bs-target="#serialCollapse-{{ Str::slug($paket->PaketName) }}-{{ $GeldiLisans->SiparisNo }}"
                                                                                                aria-expanded="{{ $isChecked ? 'true' : 'false' }}"
                                                                                            @endif
                                                                                            style="cursor: pointer;">
                                                                                    </div>
                                                                                </td>
                                                                                <td class="align-middle text-center">
                                                                                    <input type="number" class="form-control form-control-sm text-center mx-auto paket-adet-update"
                                                                                        name="adetler[{{ $paket->PaketName }}]"
                                                                                        value="{{ $adet > 0 ? $adet : 1 }}" 
                                                                                        min="1" style="max-width: 70px;"
                                                                                        {{ !$isChecked ? 'disabled' : '' }}
                                                                                        @if($paket->PaketTipi == 'yazarkasa')
                                                                                            onchange="updateSerialInputsForModal(this, '{{ Str::slug($paket->PaketName) }}-{{ $GeldiLisans->SiparisNo }}')"
                                                                                        @endif
                                                                                        >
                                                                                </td>
                                                                                <td class="pe-4 align-middle">
                                                                                    <input type="date" class="form-control form-control-sm"
                                                                                        name="tarihler[{{ $paket->PaketName }}]"
                                                                                        value="{{ $programDate }}" 
                                                                                        {{ !$isChecked ? 'disabled' : '' }}>
                                                                                </td>
                                                                            </tr>

                                                                            {{-- SERİ NUMARASI ALANI (COLLAPSE) --}}
                                                                            @if($paket->PaketTipi == 'yazarkasa')
                                                                                <tr>
                                                                                    <td colspan="4" class="p-0 border-0">
                                                                                        <div class="collapse {{ $isChecked ? 'show' : '' }}" id="serialCollapse-{{ Str::slug($paket->PaketName) }}-{{ $GeldiLisans->SiparisNo }}">
                                                                                            <div class="card card-body bg-light border-0 m-0 p-3" style="background-color: #f1f5f9 !important; border-top: 1px dashed #cbd5e1 !important;">
                                                                                                <h6 class="fs-8 fw-bold text-muted mb-2 text-uppercase ls-1">
                                                                                                    <i class="bi bi-qr-code me-1"></i> Seri Numaraları
                                                                                                </h6>
                                                                                                <div id="serialList-{{ Str::slug($paket->PaketName) }}-{{ $GeldiLisans->SiparisNo }}" class="d-flex flex-column gap-2">
                                                                                                    @for($i = 0; $i < ($adet > 0 ? $adet : 1); $i++)
                                                                                                        <div class="input-group input-group-sm">
                                                                                                            <span class="input-group-text bg-white text-muted border-end-0">{{ $i + 1 }}. Cihaz</span>
                                                                                                            <input type="text" 
                                                                                                                   class="form-control bg-white serial-input-update" 
                                                                                                                   name="serials[{{ $paket->PaketName }}][]"
                                                                                                                   value="{{ $existingSerials[$i] ?? '' }}"
                                                                                                                   placeholder="Seri Numarası Giriniz"
                                                                                                                   {{ !$isChecked ? 'disabled' : '' }}>
                                                                                                        </div>
                                                                                                    @endfor
                                                                                                </div>
                                                                                                <small class="text-muted mt-2 fst-italic" style="font-size: 0.7rem;">
                                                                                                    * Adet sayısını değiştirdiğinizde alanlar otomatik güncellenir.
                                                                                                </small>
                                                                                            </div>
                                                                                        </div>
                                                                                    </td>
                                                                                </tr>
                                                                            @endif
                                                                        @endforeach
                                                                    </tbody>
                                                                </table>
                                                            </div>
                                                        </div>
                                                    </div>
                                                @endforeach
                                            </div>
                                        </div>

                                        <div class="modal-footer">
                                            <button type="button" class="btn btn-light border" data-bs-dismiss="modal">İptal</button>
                                            
                                            <form method="POST" action="{{ route('updateWithSiparis', ['siparisNo' => $GeldiLisans->SiparisNo]) }}" id="bulkUpdateForm-{{ $GeldiLisans->SiparisNo }}">
                                                @csrf
                                                <input type="hidden" name="bulk_update" value="1">
                                                <button type="button" class="btn btn-primary fw-medium px-4" onclick="prepareAndSubmitForm('{{ $GeldiLisans->SiparisNo }}')">
                                                    <i class="bi bi-check2-circle me-2"></i> Onayla ve Bitir
                                                </button>
                                            </form>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        @endif
                    @endforeach
                </div>

                {{-- ═══════════════════════════════════════════════════════════
                                    POS SYNC TAB'LARI
                                    ═══════════════════════════════════════════════════════════ --}}

                {{-- ════════════════════════════════════════════════════════════
                    TAB: Ürünler — Modern Tasarım (Tüm POS Alanları)
                    ════════════════════════════════════════════════════════════ --}}

                <div class="tab-pane fade" id="saha" role="tabpanel">
                    <ul class="nav nav-pills saha-pills px-4 pt-3 gap-1 flex-wrap" role="tablist">
                        <li class="nav-item"><a class="nav-link active" data-bs-toggle="pill" href="#sync-urunler" role="tab">Ürünler <span class="badge bg-info">{{ $syncProducts->count() }}</span></a></li>
                        <li class="nav-item"><a class="nav-link" data-bs-toggle="pill" href="#sync-fisler" role="tab">Fişler <span class="badge bg-success">{{ $syncFisler->count() }}</span></a></li>
                        <li class="nav-item"><a class="nav-link" data-bs-toggle="pill" href="#sync-faturalar" role="tab">Faturalar <span class="badge bg-warning text-dark">{{ $syncFaturalar->count() }}</span></a></li>
                        <li class="nav-item"><a class="nav-link" data-bs-toggle="pill" href="#sync-cariler" role="tab">Cariler <span class="badge bg-secondary">{{ $syncCariler->count() }}</span></a></li>
                        <li class="nav-item"><a class="nav-link" data-bs-toggle="pill" href="#sync-masalar" role="tab">Masalar <span class="badge bg-dark">{{ $syncMasalar->count() }}</span></a></li>
                        <li class="nav-item"><a class="nav-link" data-bs-toggle="pill" href="#sync-patronlar" role="tab">Patronlar <span class="badge bg-primary">{{ $patronlar->count() }}</span></a></li>
                    </ul>
                    <div class="tab-content">
                <div class="tab-pane fade show active p-5" id="sync-urunler" role="tabpanel">

                    {{-- ÖZET KARTLARI --}}
                    @php
                        $toplamUrun     = $syncProducts->count();
                        $kategoriler    = $syncProducts->pluck('category')->filter()->unique()->values();
                        $toplamStokDeg  = $syncProducts->sum(fn($p) => (float)($p->current_stock ?? 0) * (float)($p->base_price ?? $p->price ?? 0));
                        $uyariSayisi    = $syncProducts->filter(fn($p) => (float)($p->current_stock ?? 0) <= (float)($p->stock_alert_threshold ?? 0) && (float)($p->stock_alert_threshold ?? 0) > 0)->count();
                    @endphp

                    <div class="row g-2 mb-4">
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Toplam Ürün</div>
                                <h4 class="fw-bold mb-0 mt-1">{{ $toplamUrun }}</h4>
                            </div>
                        </div>
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Kategori</div>
                                <h4 class="fw-bold mb-0 mt-1">{{ $kategoriler->count() }}</h4>
                            </div>
                        </div>
                    </div>

                    {{-- ARAMA + KATEGORİ FİLTRE --}}
                    <div class="row g-2 mb-3">
                        <div class="col-md-8">
                            <div class="input-group">
                                <span class="input-group-text bg-white"><i class="bi bi-search text-muted"></i></span>
                                <input type="text" id="productSearch" class="form-control" placeholder="Ürün adı, barkod veya stok kodu ara...">
                            </div>
                        </div>
                        <div class="col-md-4">
                            <select id="productCategoryFilter" class="form-select">
                                <option value="">— Tüm Kategoriler —</option>
                                @foreach($kategoriler as $cat)
                                    <option value="{{ $cat }}">{{ $cat }}</option>
                                @endforeach
                            </select>
                        </div>
                    </div>

                    {{-- ÜRÜN TABLOSU --}}
                    <div class="table-responsive">
                        <table class="table table-hover align-middle" id="productsTable">
                            <thead class="table-light">
                                <tr>
                                    <th style="width:50px;">#</th>
                                    <th>Ürün</th>
                                    <th>Kategori</th>
                                    <th>Stok Kod</th>
                                    <th>Barkod</th>
                                    <th class="text-end">Alış</th>
                                    <th class="text-end">Satış</th>
                                    <th class="text-center">KDV</th>
                                    <th class="text-end">Stok</th>
                                    <th class="text-center" style="width:50px;">Detay</th>
                                </tr>
                            </thead>
                            <tbody>
                                @forelse($syncProducts as $p)
                                    @php
                                        $stokMevcut = (float)($p->current_stock ?? 0);
                                        $stokUyari  = (float)($p->stock_alert_threshold ?? 0);
                                        $stokKritik = $stokUyari > 0 && $stokMevcut <= $stokUyari;
                                    @endphp
                                    <tr class="product-row"
                                        data-name="{{ mb_strtolower($p->name ?? '') }}"
                                        data-barcode="{{ mb_strtolower($p->barcode ?? '') }}"
                                        data-stockcode="{{ mb_strtolower($p->stock_code ?? '') }}"
                                        data-category="{{ $p->category ?? '' }}">
                                        <td><small class="text-muted">#{{ $p->id }}</small></td>
                                        <td>
                                            <strong>{{ $p->name }}</strong>
                                            @if($p->type && $p->type !== 'product')
                                                <span class="badge bg-light text-dark border ms-1" style="font-size:0.65rem;">{{ $p->type }}</span>
                                            @endif
                                        </td>
                                        <td><span class="badge bg-light text-dark border">{{ $p->category ?: '—' }}</span></td>
                                        <td><code style="font-size:0.75rem;">{{ $p->stock_code ?: '—' }}</code></td>
                                        <td><code style="font-size:0.75rem;">{{ $p->barcode ?: '—' }}</code></td>
                                        <td class="text-end text-muted">₺{{ number_format($p->purchase_price ?? 0, 2, ',', '.') }}</td>
                                        <td class="text-end fw-bold">₺{{ number_format($p->base_price ?? $p->price ?? 0, 2, ',', '.') }}</td>
                                        <td class="text-center">
                                            <small class="badge bg-warning bg-opacity-10 text-warning">%{{ rtrim(rtrim(number_format($p->vat ?? 0, 2, '.', ''), '0'), '.') }}</small>
                                        </td>
                                        <td class="text-end">
                                            @if($stokKritik)
                                                <span class="text-danger fw-bold" title="Stok eşiğin altında!">
                                                    <i class="bi bi-exclamation-triangle-fill me-1"></i>{{ rtrim(rtrim(number_format($stokMevcut, 2, '.', ''), '0'), '.') }}
                                                </span>
                                            @else
                                                <span>{{ rtrim(rtrim(number_format($stokMevcut, 2, '.', ''), '0'), '.') }}</span>
                                            @endif
                                            <small class="text-muted">{{ $p->unit ?? '' }}</small>
                                        </td>
                                        <td class="text-center">
                                            <button class="btn btn-sm btn-outline-info" data-bs-toggle="modal" data-bs-target="#productDetailModal-{{ $p->id }}">
                                                <i class="bi bi-eye"></i>
                                            </button>
                                        </td>
                                    </tr>
                                @empty
                                    <tr>
                                        <td colspan="10" class="text-center text-muted py-5">
                                            <i class="bi bi-inbox" style="font-size: 32px; opacity: 0.4;"></i><br>
                                            Bu müşteriden henüz ürün verisi gelmedi.
                                        </td>
                                    </tr>
                                @endforelse
                            </tbody>
                        </table>
                    </div>

                    {{-- ARAMA SONUÇ YOK --}}
                    <div id="productNoResult" class="text-center py-4 text-muted" style="display:none;">
                        <i class="bi bi-search" style="font-size: 28px; opacity:0.4;"></i><br>
                        Arama kriterlerinize uygun ürün bulunamadı.
                    </div>
                    <div id="productPagination" class="d-flex justify-content-between align-items-center mt-3 px-1"></div>
                </div>

                {{-- ════════════════════════════════════════════════════════════
                    ÜRÜN DETAY MODAL — Her ürün için
                    ════════════════════════════════════════════════════════════ --}}
                @foreach($syncProducts as $p)
                    @php
                        $barcodeList = is_array($p->barcodes) ? $p->barcodes : (json_decode($p->barcodes ?? '[]', true) ?: []);
                        $optionList  = is_array($p->options)  ? $p->options  : (json_decode($p->options  ?? '[]', true) ?: []);
                        $extraList   = is_array($p->extras)   ? $p->extras   : (json_decode($p->extras   ?? '[]', true) ?: []);
                    @endphp
                    <div class="modal fade corporate-modal" id="productDetailModal-{{ $p->id }}" tabindex="-1" aria-hidden="true">
                        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
                            <div class="modal-content">
                                <div class="modal-header">
                                    <div>
                                        <h5 class="modal-title fw-bold text-dark">{{ $p->name }}</h5>
                                        <div class="d-flex align-items-center mt-1 text-muted fs-7">
                                            <span class="badge bg-light text-dark border me-2">{{ $p->category ?: '—' }}</span>
                                            <span class="font-monospace">{{ $p->stock_code ?: '—' }}</span>
                                        </div>
                                    </div>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                </div>

                                <div class="modal-body p-4">

                                    {{-- TEMEL BİLGİLER --}}
                                    <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                        <i class="bi bi-info-circle me-1"></i> Temel Bilgiler
                                    </h6>
                                    <div class="row g-3 mb-4">
                                        <div class="col-md-4">
                                            <div class="border rounded-3 p-3 bg-light">
                                                <div class="text-muted small">Tip</div>
                                                <div class="fw-bold">{{ $p->type ?: 'product' }}</div>
                                            </div>
                                        </div>
                                        <div class="col-md-4">
                                            <div class="border rounded-3 p-3 bg-light">
                                                <div class="text-muted small">Birim</div>
                                                <div class="fw-bold">{{ $p->unit ?: 'piece' }}</div>
                                            </div>
                                        </div>
                                        <div class="col-md-4">
                                            <div class="border rounded-3 p-3 bg-light">
                                                <div class="text-muted small">Para Birimi</div>
                                                <div class="fw-bold">{{ $p->currency ?: 'TRY' }}</div>
                                            </div>
                                        </div>
                                    </div>

                                    {{-- FİYATLAR --}}
                                    <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                        <i class="bi bi-tag-fill me-1"></i> Fiyatlandırma
                                    </h6>
                                    <div class="row g-2 mb-4">
                                        @php
                                            $fiyatlar = [
                                                ['Alış',          $p->purchase_price ?? 0,                   'text-muted'],
                                                ['Satış',         $p->base_price     ?? $p->price      ?? 0, 'text-success'],
                                                ['Hızlı Satış',   $p->fast_sale_price ?? 0,                  'text-primary'],
                                                ['2. Hızlı',      $p->second_fast_sale_price ?? 0,           'text-primary'],
                                                ['3. Hızlı',      $p->third_fast_sale_price  ?? 0,           'text-primary'],
                                                ['Paket Satış',   $p->package_sale_price ?? 0,               'text-warning'],
                                            ];
                                        @endphp
                                        @foreach($fiyatlar as $f)
                                            <div class="col-md-4 col-6">
                                                <div class="border rounded-3 p-3 text-center bg-white">
                                                    <small class="text-muted">{{ $f[0] }}</small>
                                                    <div class="fw-bold {{ $f[2] }}">₺{{ number_format($f[1], 2, ',', '.') }}</div>
                                                </div>
                                            </div>
                                        @endforeach
                                    </div>

                                    {{-- KDV + STOK --}}
                                    <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                        <i class="bi bi-archive me-1"></i> Stok ve Vergi
                                    </h6>
                                    <div class="row g-2 mb-4">
                                        <div class="col-md-4 col-6">
                                            <div class="border rounded-3 p-3 text-center bg-white">
                                                <small class="text-muted">KDV Oranı</small>
                                                <div class="fw-bold text-warning">%{{ rtrim(rtrim(number_format($p->vat ?? 0, 2, '.', ''), '0'), '.') }}</div>
                                            </div>
                                        </div>
                                        <div class="col-md-4 col-6">
                                            <div class="border rounded-3 p-3 text-center bg-white">
                                                <small class="text-muted">Mevcut Stok</small>
                                                <div class="fw-bold {{ ($p->current_stock ?? 0) <= ($p->stock_alert_threshold ?? 0) && ($p->stock_alert_threshold ?? 0) > 0 ? 'text-danger' : '' }}">
                                                    {{ rtrim(rtrim(number_format($p->current_stock ?? 0, 2, '.', ''), '0'), '.') }} {{ $p->unit ?: '' }}
                                                </div>
                                            </div>
                                        </div>
                                        <div class="col-md-4 col-6">
                                            <div class="border rounded-3 p-3 text-center bg-white">
                                                <small class="text-muted">Uyarı Eşiği</small>
                                                <div class="fw-bold">{{ rtrim(rtrim(number_format($p->stock_alert_threshold ?? 0, 2, '.', ''), '0'), '.') }}</div>
                                            </div>
                                        </div>
                                    </div>

                                    {{-- BARKODLAR --}}
                                    @if(count($barcodeList) > 0)
                                        <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                            <i class="bi bi-upc me-1"></i> Barkodlar ({{ count($barcodeList) }})
                                        </h6>
                                        <div class="d-flex flex-wrap gap-2 mb-4">
                                            @foreach($barcodeList as $bc)
                                                <div class="border rounded-3 px-3 py-2 bg-light d-flex align-items-center">
                                                    <i class="bi bi-upc text-muted me-2"></i>
                                                    <code class="me-2">{{ $bc['barcode'] ?? '' }}</code>
                                                    <small class="badge bg-secondary">{{ $bc['type'] ?? 'piece' }}</small>
                                                </div>
                                            @endforeach
                                        </div>
                                    @endif

                                    {{-- OPSİYONLAR (Porsiyon, Boy vs.) --}}
                                    @if(count($optionList) > 0)
                                        <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                            <i class="bi bi-sliders me-1"></i> Opsiyonlar ({{ count($optionList) }})
                                        </h6>
                                        <div class="table-responsive mb-4">
                                            <table class="table table-sm align-middle border">
                                                <thead class="table-light">
                                                    <tr>
                                                        <th>Ad</th>
                                                        <th class="text-end">Fiyat</th>
                                                        <th class="text-end">Paket Fiyatı</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    @foreach($optionList as $opt)
                                                        <tr>
                                                            <td><strong>{{ $opt['name'] ?? '—' }}</strong></td>
                                                            <td class="text-end">₺{{ number_format($opt['price'] ?? 0, 2, ',', '.') }}</td>
                                                            <td class="text-end">₺{{ number_format($opt['packageSalePrice'] ?? $opt['package_sale_price'] ?? 0, 2, ',', '.') }}</td>
                                                        </tr>
                                                    @endforeach
                                                </tbody>
                                            </table>
                                        </div>
                                    @endif

                                    {{-- EKSTRALAR (Sosular, Soslar vs.) --}}
                                    @if(count($extraList) > 0)
                                        <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                            <i class="bi bi-plus-square me-1"></i> Ekstralar ({{ count($extraList) }})
                                        </h6>
                                        <div class="d-flex flex-wrap gap-2 mb-4">
                                            @foreach($extraList as $ex)
                                                <div class="border rounded-3 px-3 py-2 d-flex align-items-center {{ !empty($ex['default']) ? 'bg-success bg-opacity-10 border-success' : 'bg-light' }}">
                                                    <span class="fw-medium me-2">{{ $ex['name'] ?? '—' }}</span>
                                                    @if(!empty($ex['price']) && $ex['price'] > 0)
                                                        <small class="text-success">+₺{{ number_format($ex['price'], 2, ',', '.') }}</small>
                                                    @else
                                                        <small class="text-muted">Ücretsiz</small>
                                                    @endif
                                                    @if(!empty($ex['default']))
                                                        <span class="badge bg-success ms-2">Varsayılan</span>
                                                    @endif
                                                </div>
                                            @endforeach
                                        </div>
                                    @endif

                                    {{-- KAYIT BİLGİLERİ --}}
                                    <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                        <i class="bi bi-clock me-1"></i> Kayıt Bilgileri
                                    </h6>
                                    <div class="row g-2">
                                        <div class="col-md-6">
                                            <div class="border rounded-3 p-2 bg-white">
                                                <small class="text-muted">Eklenme:</small>
                                                <strong class="ms-1">{{ $p->created_at?->format('d.m.Y H:i') }}</strong>
                                            </div>
                                        </div>
                                        <div class="col-md-6">
                                            <div class="border rounded-3 p-2 bg-white">
                                                <small class="text-muted">Güncellenme:</small>
                                                <strong class="ms-1">{{ $p->updated_at?->format('d.m.Y H:i') }}</strong>
                                            </div>
                                        </div>
                                        @if($p->pos_id)
                                            <div class="col-12">
                                                <div class="border rounded-3 p-2 bg-white">
                                                    <small class="text-muted">POS ID:</small>
                                                    <code class="ms-1">{{ $p->pos_id }}</code>
                                                </div>
                                            </div>
                                        @endif
                                    </div>

                                </div>

                                <div class="modal-footer">
                                    <button type="button" class="btn btn-light" data-bs-dismiss="modal">Kapat</button>
                                </div>
                            </div>
                        </div>
                    </div>
                @endforeach

                {{-- ÜRÜN ARAMA & FİLTRELEME SCRIPT --}}
{{-- ÜRÜN ARAMA & FİLTRELEME SCRIPT --}}
<script>
(function () {
    const searchInput  = document.getElementById('productSearch');
    const categorySel  = document.getElementById('productCategoryFilter');
    const allRows      = Array.from(document.querySelectorAll('.product-row'));
    const noResult     = document.getElementById('productNoResult');
    const table        = document.getElementById('productsTable');
    const pgContainer  = document.getElementById('productPagination');
    const PER_PAGE     = 20;
    let currentPage    = 1;
    let filteredRows   = allRows.slice();

    function renderPage() {
        const start = (currentPage - 1) * PER_PAGE;
        const end   = start + PER_PAGE;
        allRows.forEach(r => r.style.display = 'none');
        filteredRows.slice(start, end).forEach(r => r.style.display = '');
        renderPagination();
    }

    function renderPagination() {
        if (!pgContainer) return;
        const total      = filteredRows.length;
        const totalPages = Math.max(1, Math.ceil(total / PER_PAGE));
        if (total <= PER_PAGE) { pgContainer.innerHTML = ''; return; }

        const s = (currentPage - 1) * PER_PAGE + 1;
        const e = Math.min(currentPage * PER_PAGE, total);

        let pages = [];
        if (totalPages <= 7) {
            for (let i = 1; i <= totalPages; i++) pages.push(i);
        } else {
            pages = [1];
            if (currentPage > 3) pages.push('...');
            for (let i = Math.max(2, currentPage - 1); i <= Math.min(totalPages - 1, currentPage + 1); i++) pages.push(i);
            if (currentPage < totalPages - 2) pages.push('...');
            pages.push(totalPages);
        }

        let html = `<small class="text-muted">${s}–${e} / ${total} ürün</small>
        <nav><ul class="pagination pagination-sm mb-0 gap-1">`;
        html += `<li class="page-item ${currentPage===1?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage-1}">‹</a></li>`;
        pages.forEach(p => {
            if (p === '...') html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
            else html += `<li class="page-item ${p===currentPage?'active':''}"><a class="page-link rounded" href="#" data-p="${p}">${p}</a></li>`;
        });
        html += `<li class="page-item ${currentPage===totalPages?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage+1}">›</a></li>`;
        html += '</ul></nav>';
        pgContainer.innerHTML = html;

        pgContainer.querySelectorAll('a[data-p]').forEach(a => {
            a.addEventListener('click', function(e) {
                e.preventDefault();
                const p = parseInt(this.dataset.p);
                if (p >= 1 && p <= totalPages) { currentPage = p; renderPage(); }
            });
        });
    }

    function filterProducts() {
        if (!searchInput || !categorySel) return;
        const q   = (searchInput.value || '').toLowerCase().trim();
        const cat = categorySel.value || '';
        filteredRows = allRows.filter(r => {
            const ms = !q ||
                (r.dataset.name      && r.dataset.name.indexOf(q) !== -1) ||
                (r.dataset.barcode   && r.dataset.barcode.indexOf(q) !== -1) ||
                (r.dataset.stockcode && r.dataset.stockcode.indexOf(q) !== -1);
            const mc = !cat || r.dataset.category === cat;
            return ms && mc;
        });
        currentPage = 1;
        if (table)    table.style.display    = filteredRows.length ? '' : 'none';
        if (noResult) noResult.style.display = filteredRows.length ? 'none' : '';
        renderPage();
    }

    if (searchInput) searchInput.addEventListener('input',  filterProducts);
    if (categorySel) categorySel.addEventListener('change', filterProducts);

    document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
        tab.addEventListener('shown.bs.tab', function(e) {
            if (e.target.getAttribute('href') === '#sync-urunler') {
                filteredRows = allRows.slice();
                currentPage  = 1;
                renderPage();
            }
        });
    });
})();
</script>

                {{-- ════════════════════════════════════════════════════════════
                    TAB: Fişler — Modern Tasarım (Detay Modal'lı)
                    ════════════════════════════════════════════════════════════ --}}
                <div class="tab-pane fade p-5" id="sync-fisler" role="tabpanel">

                    {{-- ÖZET KARTLARI --}}
                    @php
                        $toplamFis      = $syncFisler->count();
                        $toplamCiro     = $syncFisler->sum('toplam');
                        $nakitSayisi    = $syncFisler->where('odeme_tipi', 'nakit')->count();
                        $kartSayisi     = $syncFisler->where('odeme_tipi', 'kredi_karti')->count();
                    @endphp

                    <div class="row g-3 mb-4">
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Toplam Fiş</div>
                                <h4 class="fw-bold mb-0 mt-1">{{ $toplamFis }}</h4>
                            </div>
                        </div>
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Toplam Ciro</div>
                                <h4 class="fw-bold mb-0 mt-1 text-success">₺{{ number_format($toplamCiro, 2, ',', '.') }}</h4>
                            </div>
                        </div>
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Nakit Fiş</div>
                                <h4 class="fw-bold mb-0 mt-1 text-success">{{ $nakitSayisi }}</h4>
                            </div>
                        </div>
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Kredi Kartı Fiş</div>
                                <h4 class="fw-bold mb-0 mt-1 text-primary">{{ $kartSayisi }}</h4>
                            </div>
                        </div>
                    </div>

                    {{-- ARAMA + ÖDEME FİLTRE --}}
                    <div class="row g-2 mb-3">
                        <div class="col-md-8">
                            <div class="input-group">
                                <span class="input-group-text bg-white"><i class="bi bi-search text-muted"></i></span>
                                <input type="text" id="fisSearch" class="form-control" placeholder="Fiş no, masa, kasiyer ara...">
                            </div>
                        </div>
                        <div class="col-md-4">
                            <select id="fisPaymentFilter" class="form-select">
                                <option value="">— Tüm Ödemeler —</option>
                                <option value="nakit">Nakit</option>
                                <option value="kredi_karti">Kredi Kartı</option>
                                <option value="havale">Havale</option>
                            </select>
                        </div>
                    </div>

                    {{-- FİŞ TABLOSU --}}
                    <div class="table-responsive">
                        <table class="table table-hover align-middle" id="fisTable">
                            <thead class="table-light">
                                <tr>
                                    <th>Fiş No</th>
                                    <th>Adisyon</th>
                                    <th>Tarih</th>
                                    <th>Masa/Tip</th>
                                    <th>Kasiyer</th>
                                    <th>Ödeme</th>
                                    <th class="text-center">Ürün</th>
                                    <th class="text-end">Toplam</th>
                                    <th class="text-center" style="width:50px;">Detay</th>
                                </tr>
                            </thead>
                            <tbody>
                                @forelse($syncFisler as $f)
                                    <tr class="fis-row"
                                        data-fisno="{{ mb_strtolower($f->fis_no ?? '') }}"
                                        data-kasiyer="{{ mb_strtolower($f->kasiyer ?? '') }}"
                                        data-payment="{{ $f->odeme_tipi ?? '' }}">
                                        <td><code class="fw-bold">{{ $f->fis_no }}</code></td>
                                        <td>
                                            @if($f->adisyon_no)
                                                <code class="text-muted">{{ $f->adisyon_no }}</code>
                                            @else
                                                <small class="text-muted">—</small>
                                            @endif
                                        </td>
                                        <td>{{ $f->tarih?->format('d.m.Y') }}</td>
										<td>
                                            @if($f->sale_type === 'masali')
                                                <span class="badge bg-info bg-opacity-10 text-info border border-info">
                                                    <i class="bi bi-grid-3x3-gap me-1"></i>Masalı
                                                </span>
                                            @elseif($f->sale_type === 'paket')
                                                <span class="badge bg-warning bg-opacity-10 text-warning border border-warning">
                                                    <i class="bi bi-bag me-1"></i>Paket
                                                </span>
                                            @elseif($f->sale_type === 'hizli')
                                                <span class="badge bg-primary bg-opacity-10 text-primary border border-primary">
                                                    <i class="bi bi-lightning me-1"></i>Hızlı
                                                </span>
                                            @elseif($f->sale_type === 'package-sale')
                                                <span class="badge bg-success bg-opacity-10 text-success border border-success">
                                                    <i class="bi bi-box-seam me-1"></i>Paket Satış
                                                </span>
                                            @elseif($f->sale_type)
                                                <span class="badge bg-secondary bg-opacity-10 text-secondary border border-secondary">
                                                    <i class="bi bi-tag me-1"></i>{{ $f->sale_type }}
                                                </span>
                                            @else
                                                <small class="text-muted">—</small>
                                            @endif
                                        </td>
                                        <td>
                                            @if($f->kasiyer)
                                                <small><i class="bi bi-person-circle me-1 text-muted"></i>{{ $f->kasiyer }}</small>
                                            @else
                                                <small class="text-muted">—</small>
                                            @endif
                                        </td>
                                        <td>
                                            @php
                                                $renk = ['nakit' => 'success', 'kredi_karti' => 'primary', 'havale' => 'info'][$f->odeme_tipi] ?? 'secondary';
                                                $etiket = ['nakit' => 'Nakit', 'kredi_karti' => 'Kredi Kartı', 'havale' => 'Havale'][$f->odeme_tipi] ?? $f->odeme_tipi;
                                            @endphp
                                            <span class="badge bg-{{ $renk }}">{{ $etiket }}</span>
                                        </td>
                                        <td class="text-center">
                                            <span class="badge bg-light text-dark border">
                                                {{ is_array($f->urunler) ? count($f->urunler) : 0 }} kalem
                                            </span>
                                        </td>
                                        <td class="text-end">
                                            <strong class="text-success">₺{{ number_format($f->toplam, 2, ',', '.') }}</strong>
                                        </td>
                                        <td class="text-center">
                                            <button class="btn btn-sm btn-outline-info" data-bs-toggle="modal" data-bs-target="#fisDetailModal-{{ $f->id }}">
                                                <i class="bi bi-eye"></i>
                                            </button>
                                        </td>
                                    </tr>
                                @empty
                                    <tr>
                                        <td colspan="9" class="text-center text-muted py-5">
                                            <i class="bi bi-inbox" style="font-size: 32px; opacity: 0.4;"></i><br>
                                            Bu müşteriden henüz fiş verisi gelmedi.
                                        </td>
                                    </tr>
                                @endforelse
                            </tbody>
                        </table>
                    </div>

                    {{-- ARAMA SONUÇ YOK --}}
                    <div id="fisNoResult" class="text-center py-4 text-muted" style="display:none;">
                        <i class="bi bi-search" style="font-size: 28px; opacity:0.4;"></i><br>
                        Arama kriterlerinize uygun fiş bulunamadı.
                    </div>
                    <div id="fisPagination" class="d-flex justify-content-between align-items-center mt-3 px-1"></div>
                </div>

                {{-- ════════════════════════════════════════════════════════════
                    FİŞ DETAY MODAL — Her fiş için
                    ════════════════════════════════════════════════════════════ --}}
                @foreach($syncFisler as $f)
                    @php
                        $fisUrunler = is_array($f->urunler) ? $f->urunler : (json_decode($f->urunler ?? '[]', true) ?: []);
                        $fisOdemeler = is_array($f->odemeler) ? $f->odemeler : (json_decode($f->odemeler ?? '[]', true) ?: []);
                        $araToplam = collect($fisUrunler)->sum(fn($u) => (float)($u['adet'] ?? 1) * (float)($u['fiyat'] ?? 0));
                    @endphp
                    <div class="modal fade corporate-modal" id="fisDetailModal-{{ $f->id }}" tabindex="-1" aria-hidden="true">
                        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
                            <div class="modal-content">
                                <div class="modal-header">
                                    <div>
                                        <h5 class="modal-title fw-bold text-dark">
                                            <i class="bi bi-receipt me-2 text-success"></i>{{ $f->fis_no }}
                                        </h5>
                                        <div class="d-flex align-items-center mt-1 text-muted fs-7 flex-wrap gap-2">
                                            @if($f->adisyon_no)
                                                <span class="badge bg-light text-dark border">{{ $f->adisyon_no }}</span>
                                            @endif
                                            <span><i class="bi bi-calendar3 me-1"></i>{{ $f->tarih?->format('d.m.Y') }}</span>
                                            @if($f->kasiyer)
                                                <span><i class="bi bi-person me-1"></i>{{ $f->kasiyer }}</span>
                                            @endif
                                        </div>
                                    </div>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                </div>

                                <div class="modal-body p-4">

                                    {{-- ÖZET KARTLAR --}}
                                    <div class="row g-2 mb-4">
                                        <div class="col-md-4 col-6">
                                            <div class="border rounded-3 p-3 text-center bg-light">
                                                <small class="text-muted">Satış Tipi</small>
                                                <div class="fw-bold">
                                                    @if($f->sale_type === 'masali')
                                                        <i class="bi bi-grid-3x3-gap me-1"></i>Masalı
                                                    @elseif($f->sale_type === 'paket')
                                                        <i class="bi bi-bag me-1"></i>Paket
                                                    @elseif($f->sale_type === 'hizli')
                                                        <i class="bi bi-lightning me-1"></i>Hızlı
                                                    @else
                                                        {{ $f->sale_type ?: '—' }}
                                                    @endif
                                                </div>
                                            </div>
                                        </div>
                                        <div class="col-md-4 col-6">
                                            <div class="border rounded-3 p-3 text-center bg-light">
                                                <small class="text-muted">Kişi Sayısı</small>
                                                <div class="fw-bold">{{ $f->kisi_sayisi ?? 1 }} kişi</div>
                                            </div>
                                        </div>
                                        <div class="col-md-4 col-12">
                                            <div class="border rounded-3 p-3 text-center bg-light">
                                                <small class="text-muted">Ürün Kalemi</small>
                                                <div class="fw-bold">{{ count($fisUrunler) }} adet</div>
                                            </div>
                                        </div>
                                    </div>

                                    {{-- ÜRÜN KALEMLERİ --}}
                                    <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                        <i class="bi bi-box-seam me-1"></i> Ürün Kalemleri
                                    </h6>
                                    <div class="table-responsive mb-4">
                                        <table class="table table-sm align-middle border">
                                            <thead class="table-light">
                                                <tr>
                                                    <th>Ürün</th>
                                                    <th>Kategori</th>
                                                    <th class="text-center">Adet</th>
                                                    <th class="text-end">B. Fiyat</th>
                                                    <th class="text-center">KDV</th>
                                                    <th class="text-end">Tutar</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                @forelse($fisUrunler as $u)
                                                    @php
                                                        $adet = (float)($u['adet'] ?? 1);
                                                        $fiyat = (float)($u['fiyat'] ?? 0);
                                                        $satirToplam = $adet * $fiyat;
                                                    @endphp
                                                    <tr>
                                                        <td>
                                                            <strong>{{ $u['name'] ?? '—' }}</strong>
                                                            @if(!empty($u['stok_kodu']))
                                                                <br><small class="text-muted font-monospace">{{ $u['stok_kodu'] }}</small>
                                                            @endif
                                                            @if(!empty($u['not']))
                                                                <br><small class="text-warning"><i class="bi bi-sticky"></i> {{ $u['not'] }}</small>
                                                            @endif
                                                        </td>
                                                        <td><small class="badge bg-light text-dark border">{{ $u['kategori'] ?? '—' }}</small></td>
                                                        <td class="text-center"><strong>{{ rtrim(rtrim(number_format($adet, 2, '.', ''), '0'), '.') }}</strong></td>
                                                        <td class="text-end">₺{{ number_format($fiyat, 2, ',', '.') }}</td>
                                                        <td class="text-center">
                                                            <small class="text-warning">%{{ rtrim(rtrim(number_format($u['kdv'] ?? 0, 2, '.', ''), '0'), '.') }}</small>
                                                        </td>
                                                        <td class="text-end fw-bold">₺{{ number_format($satirToplam, 2, ',', '.') }}</td>
                                                    </tr>
                                                @empty
                                                    <tr>
                                                        <td colspan="6" class="text-center text-muted py-3">Ürün kalemi yok</td>
                                                    </tr>
                                                @endforelse
                                            </tbody>
                                        </table>
                                    </div>

                                    {{-- ÖDEME DETAYLARI --}}
                                    @if(count($fisOdemeler) > 0)
                                        <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                            <i class="bi bi-cash-stack me-1"></i> Ödeme Detayları ({{ count($fisOdemeler) }})
                                        </h6>
                                        <div class="d-flex flex-wrap gap-2 mb-4">
                                            @foreach($fisOdemeler as $od)
                                                @php
                                                    $tip = $od['paymentType'] ?? $od['tip'] ?? '—';
                                                    $tipLower = mb_strtolower($tip);
                                                    $iconClass = str_contains($tipLower, 'kredi') || str_contains($tipLower, 'kart') ? 'bi-credit-card text-primary' 
                                                               : (str_contains($tipLower, 'havale') ? 'bi-bank text-info' 
                                                               : 'bi-cash text-success');
                                                    $borderClass = str_contains($tipLower, 'kredi') || str_contains($tipLower, 'kart') ? 'border-primary bg-primary bg-opacity-10' 
                                                                 : (str_contains($tipLower, 'havale') ? 'border-info bg-info bg-opacity-10' 
                                                                 : 'border-success bg-success bg-opacity-10');
                                                @endphp
                                                <div class="border rounded-3 px-3 py-2 d-flex align-items-center {{ $borderClass }}">
                                                    <i class="bi {{ $iconClass }} me-2 fs-5"></i>
                                                    <div>
                                                        <div class="fw-bold">{{ $tip }}</div>
                                                        <small class="text-muted">₺{{ number_format($od['tutar'] ?? $od['price'] ?? 0, 2, ',', '.') }}</small>
                                                    </div>
                                                </div>
                                            @endforeach
                                        </div>
                                    @endif

                                    {{-- TOPLAM ÖZETI --}}
                                    <h6 class="text-uppercase text-muted fw-bold mb-3" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                        <i class="bi bi-calculator me-1"></i> Hesap Özeti
                                    </h6>
                                    <div class="border rounded-3 p-3 bg-light">
                                        <div class="d-flex justify-content-between mb-2">
                                            <span class="text-muted">Ara Toplam:</span>
                                            <strong>₺{{ number_format($f->ara_toplam ?? $araToplam, 2, ',', '.') }}</strong>
                                        </div>
                                        @if(($f->iskonto ?? 0) > 0)
                                            <div class="d-flex justify-content-between mb-2 text-danger">
                                                <span>İskonto:</span>
                                                <strong>-₺{{ number_format($f->iskonto, 2, ',', '.') }}</strong>
                                            </div>
                                        @endif
                                        <hr class="my-2">
                                        <div class="d-flex justify-content-between">
                                            <span class="fw-bold fs-5">GENEL TOPLAM:</span>
                                            <strong class="text-success fs-5">₺{{ number_format($f->toplam, 2, ',', '.') }}</strong>
                                        </div>
                                    </div>

                                    {{-- KAYIT BİLGİLERİ --}}
                                    @if($f->pos_id || $f->created_at)
                                        <h6 class="text-uppercase text-muted fw-bold mb-3 mt-4" style="font-size: 0.7rem; letter-spacing: 0.05em;">
                                            <i class="bi bi-clock me-1"></i> Kayıt Bilgileri
                                        </h6>
                                        <div class="row g-2">
                                            @if($f->created_at)
                                                <div class="col-md-6">
                                                    <div class="border rounded-3 p-2 bg-white">
                                                        <small class="text-muted">Senkron:</small>
                                                        <strong class="ms-1">{{ $f->created_at?->format('d.m.Y H:i') }}</strong>
                                                    </div>
                                                </div>
                                            @endif
                                            @if($f->pos_id)
                                                <div class="col-md-6">
                                                    <div class="border rounded-3 p-2 bg-white">
                                                        <small class="text-muted">POS ID:</small>
                                                        <code class="ms-1">{{ $f->pos_id }}</code>
                                                    </div>
                                                </div>
                                            @endif
                                        </div>
                                    @endif

                                </div>

                                <div class="modal-footer">
                                    <button type="button" class="btn btn-light" data-bs-dismiss="modal">Kapat</button>
                                </div>
                            </div>
                        </div>
                    </div>
                @endforeach

                {{-- FİŞ ARAMA & FİLTRELEME SCRIPT --}}
               <script>
                (function () {
                    const fisSearch   = document.getElementById('fisSearch');
                    const fisPayment  = document.getElementById('fisPaymentFilter');
                    const allRows     = Array.from(document.querySelectorAll('.fis-row'));
                    const fisNoResult = document.getElementById('fisNoResult');
                    const fisTable    = document.getElementById('fisTable');
                    const pgContainer = document.getElementById('fisPagination');
                    const PER_PAGE    = 20;
                    let currentPage   = 1;
                    let filteredRows  = allRows.slice();

                    function renderPage() {
                        const start = (currentPage - 1) * PER_PAGE;
                        const end   = start + PER_PAGE;
                        allRows.forEach(r => r.style.display = 'none');
                        filteredRows.slice(start, end).forEach(r => r.style.display = '');
                        renderPagination();
                    }

                    function renderPagination() {
                        if (!pgContainer) return;
                        const total      = filteredRows.length;
                        const totalPages = Math.max(1, Math.ceil(total / PER_PAGE));
                        if (total <= PER_PAGE) { pgContainer.innerHTML = ''; return; }

                        const s = (currentPage - 1) * PER_PAGE + 1;
                        const e = Math.min(currentPage * PER_PAGE, total);

                        let pages = [];
                        if (totalPages <= 7) {
                            for (let i = 1; i <= totalPages; i++) pages.push(i);
                        } else {
                            pages = [1];
                            if (currentPage > 3) pages.push('...');
                            for (let i = Math.max(2, currentPage - 1); i <= Math.min(totalPages - 1, currentPage + 1); i++) pages.push(i);
                            if (currentPage < totalPages - 2) pages.push('...');
                            pages.push(totalPages);
                        }

                        let html = `<small class="text-muted">${s}–${e} / ${total} fiş</small>
                        <nav><ul class="pagination pagination-sm mb-0 gap-1">`;
                        html += `<li class="page-item ${currentPage===1?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage-1}">‹</a></li>`;
                        pages.forEach(p => {
                            if (p === '...') html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
                            else html += `<li class="page-item ${p===currentPage?'active':''}"><a class="page-link rounded" href="#" data-p="${p}">${p}</a></li>`;
                        });
                        html += `<li class="page-item ${currentPage===totalPages?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage+1}">›</a></li>`;
                        html += '</ul></nav>';
                        pgContainer.innerHTML = html;

                        pgContainer.querySelectorAll('a[data-p]').forEach(a => {
                            a.addEventListener('click', function(e) {
                                e.preventDefault();
                                const p = parseInt(this.dataset.p);
                                if (p >= 1 && p <= totalPages) { currentPage = p; renderPage(); }
                            });
                        });
                    }

                    function filterFisler() {
                        if (!fisSearch || !fisPayment) return;
                        const q   = (fisSearch.value || '').toLowerCase().trim();
                        const pay = fisPayment.value || '';
                        filteredRows = allRows.filter(r => {
                            const ms = !q ||
                                (r.dataset.fisno   && r.dataset.fisno.indexOf(q) !== -1) ||
                                (r.dataset.kasiyer && r.dataset.kasiyer.indexOf(q) !== -1);
                            const mp = !pay || r.dataset.payment === pay;
                            return ms && mp;
                        });
                        currentPage = 1;
                        if (fisTable)    fisTable.style.display    = filteredRows.length ? '' : 'none';
                        if (fisNoResult) fisNoResult.style.display = filteredRows.length ? 'none' : '';
                        renderPage();
                    }

                    if (fisSearch)  fisSearch.addEventListener('input',  filterFisler);
                    if (fisPayment) fisPayment.addEventListener('change', filterFisler);

                    document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
                        tab.addEventListener('shown.bs.tab', function(e) {
                            if (e.target.getAttribute('href') === '#sync-fisler') {
                                filteredRows = allRows.slice();
                                currentPage  = 1;
                                renderPage();
                            }
                        });
                    });
                })();
                </script>
				<script>
				(function () {
					const allRows     = Array.from(document.querySelectorAll('.fatura-row'));
					const pgContainer = document.getElementById('faturaPagination');
					const PER_PAGE    = 20;
					let currentPage   = 1;
					let filteredRows  = allRows.slice();

					function renderPage() {
						const start = (currentPage - 1) * PER_PAGE;
						const end   = start + PER_PAGE;
						allRows.forEach(r => r.style.display = 'none');
						filteredRows.slice(start, end).forEach(r => r.style.display = '');
						renderPagination();
					}

					function renderPagination() {
						if (!pgContainer) return;
						const total      = filteredRows.length;
						const totalPages = Math.max(1, Math.ceil(total / PER_PAGE));
						if (totalPages <= 1) { pgContainer.innerHTML = ''; return; }

						const s = (currentPage - 1) * PER_PAGE + 1;
						const e = Math.min(currentPage * PER_PAGE, total);

						let pages = [];
						if (totalPages <= 7) {
							for (let i = 1; i <= totalPages; i++) pages.push(i);
						} else {
							pages = [1];
							if (currentPage > 3) pages.push('...');
							for (let i = Math.max(2, currentPage - 1); i <= Math.min(totalPages - 1, currentPage + 1); i++) pages.push(i);
							if (currentPage < totalPages - 2) pages.push('...');
							pages.push(totalPages);
						}

						let html = `<small class="text-muted">${s}–${e} / ${total} fatura</small>
						<nav><ul class="pagination pagination-sm mb-0 gap-1">`;
						html += `<li class="page-item ${currentPage===1?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage-1}">‹</a></li>`;
						pages.forEach(p => {
							if (p === '...') html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
							else html += `<li class="page-item ${p===currentPage?'active':''}"><a class="page-link rounded" href="#" data-p="${p}">${p}</a></li>`;
						});
						html += `<li class="page-item ${currentPage===totalPages?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage+1}">›</a></li>`;
						html += '</ul></nav>';
						pgContainer.innerHTML = html;

						pgContainer.querySelectorAll('a[data-p]').forEach(a => {
							a.addEventListener('click', function(e) {
								e.preventDefault();
								const p = parseInt(this.dataset.p);
								if (p >= 1 && p <= totalPages) { currentPage = p; renderPage(); }
							});
						});
					}

					document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
						tab.addEventListener('shown.bs.tab', function(e) {
							if (e.target.getAttribute('href') === '#sync-faturalar') {
								filteredRows = allRows.slice();
								currentPage  = 1;
								renderPage();
							}
						});
					});
				})();
				</script>
                {{-- TAB: Faturalar --}}
                <div class="tab-pane fade p-5" id="sync-faturalar" role="tabpanel">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h5 class="mb-0"><i class="bi bi-file-earmark-ruled me-2 text-warning"></i>POS'tan Senkronlanan Faturalar</h5>
                        <small class="text-muted">Toplam: <strong>{{ $syncFaturalar->count() }}</strong> fatura</small>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-hover align-middle">
                            <thead class="table-light">
                                <tr>
                                    <th>Fatura No</th>
                                    <th>Cari</th>
                                    <th>Tarih</th>
                                    <th class="text-end">KDV</th>
                                    <th class="text-end">Toplam</th>
                                </tr>
                            </thead>
                            <tbody>
                                @forelse($syncFaturalar as $f)
                                    <tr class="fatura-row">
                                        <td><code>{{ $f->fatura_no }}</code></td>
                                        <td><strong>{{ $f->cari ?: '—' }}</strong></td>
                                        <td>{{ $f->tarih?->format('d.m.Y') }}</td>
                                        <td class="text-end text-muted">₺{{ number_format($f->kdv, 2, ',', '.') }}</td>
                                        <td class="text-end"><strong>₺{{ number_format($f->toplam, 2, ',', '.') }}</strong></td>
                                    </tr>
                                @empty
                                    <tr>
                                        <td colspan="5" class="text-center text-muted py-5">
                                            <i class="bi bi-inbox" style="font-size: 32px; opacity: 0.4;"></i><br>
                                            Bu müşteriden henüz fatura verisi gelmedi.
                                        </td>
                                    </tr>
                                @endforelse
                            </tbody>
                        </table>
                    </div>
                    <div id="faturaPagination" class="d-flex justify-content-between align-items-center mt-3 px-1"></div>

                </div>

				<script>
				(function () {
					const allRows     = Array.from(document.querySelectorAll('.cari-row'));
					const pgContainer = document.getElementById('cariPagination');
					const PER_PAGE    = 20;
					let currentPage   = 1;
					let filteredRows  = allRows.slice();

					function renderPage() {
						const start = (currentPage - 1) * PER_PAGE;
						const end   = start + PER_PAGE;
						allRows.forEach(r => r.style.display = 'none');
						filteredRows.slice(start, end).forEach(r => r.style.display = '');
						renderPagination();
					}

					function renderPagination() {
						if (!pgContainer) return;
						const total      = filteredRows.length;
						const totalPages = Math.max(1, Math.ceil(total / PER_PAGE));
						if (totalPages <= 1) { pgContainer.innerHTML = ''; return; }

						const s = (currentPage - 1) * PER_PAGE + 1;
						const e = Math.min(currentPage * PER_PAGE, total);

						let pages = [];
						if (totalPages <= 7) {
							for (let i = 1; i <= totalPages; i++) pages.push(i);
						} else {
							pages = [1];
							if (currentPage > 3) pages.push('...');
							for (let i = Math.max(2, currentPage - 1); i <= Math.min(totalPages - 1, currentPage + 1); i++) pages.push(i);
							if (currentPage < totalPages - 2) pages.push('...');
							pages.push(totalPages);
						}

						let html = `<small class="text-muted">${s}–${e} / ${total} cari</small>
						<nav><ul class="pagination pagination-sm mb-0 gap-1">`;
						html += `<li class="page-item ${currentPage===1?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage-1}">‹</a></li>`;
						pages.forEach(p => {
							if (p === '...') html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
							else html += `<li class="page-item ${p===currentPage?'active':''}"><a class="page-link rounded" href="#" data-p="${p}">${p}</a></li>`;
						});
						html += `<li class="page-item ${currentPage===totalPages?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage+1}">›</a></li>`;
						html += '</ul></nav>';
						pgContainer.innerHTML = html;

						pgContainer.querySelectorAll('a[data-p]').forEach(a => {
							a.addEventListener('click', function(e) {
								e.preventDefault();
								const p = parseInt(this.dataset.p);
								if (p >= 1 && p <= totalPages) { currentPage = p; renderPage(); }
							});
						});
					}

					document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
						tab.addEventListener('shown.bs.tab', function(e) {
							if (e.target.getAttribute('href') === '#sync-cariler') {
								filteredRows = allRows.slice();
								currentPage  = 1;
								renderPage();
							}
						});
					});
				})();
				</script>

                {{-- TAB: Cariler --}}
                <div class="tab-pane fade p-5" id="sync-cariler" role="tabpanel">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h5 class="mb-0"><i class="bi bi-people me-2 text-secondary"></i>POS'tan Senkronlanan Cariler</h5>
                        <small class="text-muted">Toplam: <strong>{{ $syncCariler->count() }}</strong> cari</small>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-hover align-middle">
                            <thead class="table-light">
                                <tr>
                                    <th>Ünvan</th>
                                    <th>Vergi No</th>
                                    <th>Vergi Dairesi</th>
                                    <th>Telefon</th>
                                    <th class="text-end">Bakiye</th>
                                </tr>
                            </thead>
                            <tbody>
                                @forelse($syncCariler as $c)
                                    <tr class="cari-row">
                                        <td><strong>{{ $c->unvan }}</strong></td>
                                        <td><code>{{ $c->vergi_no ?: '—' }}</code></td>
                                        <td>{{ $c->vergi_dairesi ?: '—' }}</td>
                                        <td>{{ $c->telefon ?: '—' }}</td>
                                        <td class="text-end">
                                            <strong class="{{ $c->bakiye < 0 ? 'text-danger' : 'text-success' }}">
                                                ₺{{ number_format($c->bakiye, 2, ',', '.') }}
                                            </strong>
                                        </td>
                                    </tr>
                                @empty
                                    <tr>
                                        <td colspan="5" class="text-center text-muted py-5">
                                            <i class="bi bi-inbox" style="font-size: 32px; opacity: 0.4;"></i><br>
                                            Bu müşteriden henüz cari verisi gelmedi.
                                        </td>
                                    </tr>
                                @endforelse
                            </tbody>
                        </table>
                    </div>
                    <div id="cariPagination" class="d-flex justify-content-between align-items-center mt-3 px-1"></div>
                </div>

				<script>
				(function () {
					const allRows     = Array.from(document.querySelectorAll('.masa-row'));
					const pgContainer = document.getElementById('masaPagination');
					const PER_PAGE    = 20;
					let currentPage   = 1;
					let filteredRows  = allRows.slice();

					function renderPage() {
						const start = (currentPage - 1) * PER_PAGE;
						const end   = start + PER_PAGE;
						allRows.forEach(r => r.style.display = 'none');
						filteredRows.slice(start, end).forEach(r => r.style.display = '');
						renderPagination();
					}

					function renderPagination() {
						if (!pgContainer) return;
						const total      = filteredRows.length;
						const totalPages = Math.max(1, Math.ceil(total / PER_PAGE));
						if (totalPages <= 1) { pgContainer.innerHTML = ''; return; }

						const s = (currentPage - 1) * PER_PAGE + 1;
						const e = Math.min(currentPage * PER_PAGE, total);

						let pages = [];
						if (totalPages <= 7) {
							for (let i = 1; i <= totalPages; i++) pages.push(i);
						} else {
							pages = [1];
							if (currentPage > 3) pages.push('...');
							for (let i = Math.max(2, currentPage - 1); i <= Math.min(totalPages - 1, currentPage + 1); i++) pages.push(i);
							if (currentPage < totalPages - 2) pages.push('...');
							pages.push(totalPages);
						}

						let html = `<small class="text-muted">${s}–${e} / ${total} masa</small>
						<nav><ul class="pagination pagination-sm mb-0 gap-1">`;
						html += `<li class="page-item ${currentPage===1?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage-1}">‹</a></li>`;
						pages.forEach(p => {
							if (p === '...') html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
							else html += `<li class="page-item ${p===currentPage?'active':''}"><a class="page-link rounded" href="#" data-p="${p}">${p}</a></li>`;
						});
						html += `<li class="page-item ${currentPage===totalPages?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage+1}">›</a></li>`;
						html += '</ul></nav>';
						pgContainer.innerHTML = html;

						pgContainer.querySelectorAll('a[data-p]').forEach(a => {
							a.addEventListener('click', function(e) {
								e.preventDefault();
								const p = parseInt(this.dataset.p);
								if (p >= 1 && p <= totalPages) { currentPage = p; renderPage(); }
							});
						});
					}

					document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
						tab.addEventListener('shown.bs.tab', function(e) {
							if (e.target.getAttribute('href') === '#sync-masalar') {
								filteredRows = allRows.slice();
								currentPage  = 1;
								renderPage();
							}
						});
					});
				})();
				</script>

                {{-- TAB: Masalar --}}
                <div class="tab-pane fade p-5" id="sync-masalar" role="tabpanel">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h5 class="mb-0"><i class="bi bi-grid-3x3-gap me-2 text-dark"></i>POS'tan Senkronlanan Masalar</h5>
                        <small class="text-muted">Toplam: <strong>{{ $syncMasalar->count() }}</strong> masa</small>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-hover align-middle">
                            <thead class="table-light">
                                <tr>
                                    <th>Masa No</th>
                                    <th>Ad</th>
                                    <th>Bölge</th>
                                    <th>Kapasite</th>
                                    <th>Durum</th>
                                    <th>Açık Hesap</th>
                                </tr>
                            </thead>
                            <tbody>
                                @forelse($syncMasalar as $m)
                                    <tr class="masa-row">
                                        <td><code>{{ $m->masa_no }}</code></td>
                                        <td><strong>{{ $m->ad ?: '—' }}</strong></td>
                                        <td>{{ $m->bolge ?: '—' }}</td>
                                        <td>{{ $m->kapasite }} kişi</td>
                                        <td>
                                            @if($m->durum === 'dolu')
                                                <span class="badge bg-danger">🔴 Dolu</span>
                                            @else
                                                <span class="badge bg-success">🟢 Boş</span>
                                            @endif
                                        </td>
                                        <td>
                                            @if(is_array($m->urunler) && count($m->urunler))
                                                <small><strong>{{ count($m->urunler) }}</strong> kalem ürün</small>
                                            @else
                                                <small class="text-muted">—</small>
                                            @endif
                                        </td>
                                    </tr>
                                @empty
                                    <tr>
                                        <td colspan="6" class="text-center text-muted py-5">
                                            <i class="bi bi-inbox" style="font-size: 32px; opacity: 0.4;"></i><br>
                                            Bu müşteriden henüz masa verisi gelmedi.
                                        </td>
                                    </tr>
                                @endforelse
                            </tbody>
                        </table>
                    </div>
                    <div id="masaPagination" class="d-flex justify-content-between align-items-center mt-3 px-1"></div>
                </div>

				<script>
				(function () {
					const allRows     = Array.from(document.querySelectorAll('.masa-row'));
					const pgContainer = document.getElementById('masaPagination');
					const PER_PAGE    = 20;
					let currentPage   = 1;
					let filteredRows  = allRows.slice();

					function renderPage() {
						const start = (currentPage - 1) * PER_PAGE;
						const end   = start + PER_PAGE;
						allRows.forEach(r => r.style.display = 'none');
						filteredRows.slice(start, end).forEach(r => r.style.display = '');
						renderPagination();
					}

					function renderPagination() {
						if (!pgContainer) return;
						const total      = filteredRows.length;
						const totalPages = Math.max(1, Math.ceil(total / PER_PAGE));
						if (totalPages <= 1) { pgContainer.innerHTML = ''; return; }

						const s = (currentPage - 1) * PER_PAGE + 1;
						const e = Math.min(currentPage * PER_PAGE, total);

						let pages = [];
						if (totalPages <= 7) {
							for (let i = 1; i <= totalPages; i++) pages.push(i);
						} else {
							pages = [1];
							if (currentPage > 3) pages.push('...');
							for (let i = Math.max(2, currentPage - 1); i <= Math.min(totalPages - 1, currentPage + 1); i++) pages.push(i);
							if (currentPage < totalPages - 2) pages.push('...');
							pages.push(totalPages);
						}

						let html = `<small class="text-muted">${s}–${e} / ${total} masa</small>
						<nav><ul class="pagination pagination-sm mb-0 gap-1">`;
						html += `<li class="page-item ${currentPage===1?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage-1}">‹</a></li>`;
						pages.forEach(p => {
							if (p === '...') html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
							else html += `<li class="page-item ${p===currentPage?'active':''}"><a class="page-link rounded" href="#" data-p="${p}">${p}</a></li>`;
						});
						html += `<li class="page-item ${currentPage===totalPages?'disabled':''}"><a class="page-link rounded" href="#" data-p="${currentPage+1}">›</a></li>`;
						html += '</ul></nav>';
						pgContainer.innerHTML = html;

						pgContainer.querySelectorAll('a[data-p]').forEach(a => {
							a.addEventListener('click', function(e) {
								e.preventDefault();
								const p = parseInt(this.dataset.p);
								if (p >= 1 && p <= totalPages) { currentPage = p; renderPage(); }
							});
						});
					}

					document.querySelectorAll('a[data-bs-toggle="tab"]').forEach(tab => {
						tab.addEventListener('shown.bs.tab', function(e) {
							if (e.target.getAttribute('href') === '#sync-masalar') {
								filteredRows = allRows.slice();
								currentPage  = 1;
								renderPage();
							}
						});
					});
				})();
				</script>

				{{-- ════════════════════════════════════════════════════════════
                    TAB: Patronlar — Modern Tasarım
                    ════════════════════════════════════════════════════════════ --}}
                <div class="tab-pane fade p-5" id="sync-patronlar" role="tabpanel">

                    <div class="d-flex justify-content-between align-items-center mb-4">
                        <h5 class="mb-0 fw-bold">
                            <i class="bi bi-person-badge me-2 text-primary"></i>Patron Hesapları
                        </h5>
                    </div>

                    @if(count($musteriLisansAnahtarlari) === 0)
                        <div class="text-center py-5">
                            <i class="bi bi-key-fill text-muted" style="font-size: 48px; opacity: 0.3;"></i>
                            <p class="text-muted mt-3 mb-0">Bu müşteriye henüz lisans tanımlanmamış.</p>
                        </div>
                    @else
                        {{-- Mevcut tekil patron kaydını bul (varsa) --}}
                        @php
                            $tekPatron = $patronlar->first();
                            $mevcutLisanslar = $tekPatron 
                                ? (json_decode($tekPatron->lisans_anahtarlari ?? '[]', true) ?: [$tekPatron->lisans_anahtar])
                                : [];
                            $izinlerMevcut = $tekPatron ? (json_decode($tekPatron->izinler ?? '{}', true) ?: []) : [];
                        @endphp

                        <form method="POST" action="{{ $tekPatron ? route('patron.update', $tekPatron->id) : route('patron.add') }}">
                            @csrf
                            {{-- Tüm lisans anahtarlarını hidden olarak gönder --}}
                            <input type="hidden" name="lisans_anahtarlari" 
                                value="{{ implode(',', $musteriLisansAnahtarlari) }}">
                            <input type="hidden" name="lisans_anahtar" 
                                value="{{ $musteriLisansAnahtarlari[0] ?? '' }}">

                            <div class="card border-0 shadow-sm rounded-4 overflow-hidden">
                                <div class="card-header bg-white border-bottom px-4 py-3 d-flex justify-content-between align-items-center">
                                    <h6 class="fw-bold mb-0">
                                        <i class="bi bi-person-circle me-2 text-primary"></i>Patron Bilgileri
                                    </h6>
                                    @if($tekPatron)
                                        <span class="badge bg-success bg-opacity-10 text-success rounded-pill px-3 py-2">
                                            <i class="bi bi-check-circle me-1"></i> Hesap Aktif
                                        </span>
                                    @else
                                        <span class="badge bg-warning bg-opacity-10 text-warning rounded-pill px-3 py-2">
                                            <i class="bi bi-exclamation-circle me-1"></i> Hesap Yok
                                        </span>
                                    @endif
                                </div>

                                <div class="card-body p-4">

                                    {{-- KULLANICI ADI + AD SOYAD --}}
                                    <div class="row g-3 mb-3">
                                        <div class="col-md-6">
                                            <label class="form-label fw-bold text-uppercase text-muted" style="font-size:0.7rem;">Kullanıcı Adı</label>
                                            <input type="text" name="kullanici_adi" class="form-control"
                                                value="{{ $tekPatron->kullanici_adi ?? '' }}"
                                                placeholder="patron.ahmet" required>
                                        </div>
                                        <div class="col-md-6">
                                            <label class="form-label fw-bold text-uppercase text-muted" style="font-size:0.7rem;">Ad Soyad</label>
                                            <input type="text" name="ad_soyad" class="form-control"
                                                value="{{ $tekPatron->ad_soyad ?? '' }}"
                                                placeholder="Ahmet Yılmaz">
                                        </div>
                                    </div>

                                    {{-- ŞİFRE + DURUM --}}
                                    <div class="row g-3 mb-4">
                                        <div class="col-md-6">
                                            <label class="form-label fw-bold text-uppercase text-muted" style="font-size:0.7rem;">Şifre</label>
                                            <input type="text" name="sifre" class="form-control"
                                                placeholder="{{ $tekPatron ? 'Boş bırak = şifre korunur' : 'En az 4 karakter' }}"
                                                {{ $tekPatron ? '' : 'required' }} minlength="4">
                                            <small class="text-muted">{{ $tekPatron ? 'Yeni şifre yazmazsan eski korunur.' : 'Zorunlu.' }}</small>
                                        </div>
                                        <div class="col-md-6">
                                            <label class="form-label fw-bold text-uppercase text-muted" style="font-size:0.7rem;">Hesap Durumu</label>
                                            <div class="d-flex align-items-center p-2 border rounded" style="height:42px;">
                                                <div class="form-check form-switch m-0">
                                                    <input class="form-check-input" type="checkbox" role="switch"
                                                        id="aktif_patron" name="aktif"
                                                        style="width:44px;height:22px;cursor:pointer;"
                                                        {{ ($tekPatron && $tekPatron->aktif) || !$tekPatron ? 'checked' : '' }}>
                                                    <label class="form-check-label fw-bold ms-2" for="aktif_patron">HESAP AKTİF</label>
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    {{-- ŞUBELERİ GÖSTER --}}
                                    <div class="mb-4">
                                        <label class="form-label fw-bold text-uppercase text-muted mb-3" style="font-size:0.7rem;">
                                            <i class="bi bi-diagram-3 me-1"></i> Erişebileceği Şubeler (Lisanslar)
                                        </label>
                                        <div class="row g-2">
                                            @foreach($musteriLisansAnahtarlari as $anahtar)
                                                @php
                                                    $lisansModel = $Lisanslar->where('Anahtar', $anahtar)->first();
                                                    $subeAdi = $lisansModel->PcName ?? $anahtar;
                                                    $secili = in_array($anahtar, $mevcutLisanslar);
                                                @endphp
                                                <div class="col-md-6">
                                                    <label class="d-flex align-items-center p-3 border rounded-3 sube-card" 
                                                        style="cursor:pointer;transition:all 0.2s;">
                                                        <input class="form-check-input m-0 me-3" type="checkbox"
                                                            name="secili_lisanslar[]"
                                                            value="{{ $anahtar }}"
                                                            {{ $secili ? 'checked' : '' }}>
                                                        <div>
                                                            <div class="fw-bold text-dark">{{ $subeAdi }}</div>
                                                            <small class="font-monospace text-muted">{{ $anahtar }}</small>
                                                        </div>
                                                    </label>
                                                </div>
                                            @endforeach
                                        </div>
                                    </div>

                                    {{-- İZİNLER --}}
                                    <div class="mb-2 d-flex justify-content-between align-items-center">
                                        <label class="form-label fw-bold text-uppercase text-muted mb-0" style="font-size:0.7rem;">
                                            <i class="bi bi-shield-check me-1"></i> Görünür Modüller
                                        </label>
                                        <button type="button" class="btn btn-sm btn-light border" onclick="toggleAllPermissions(this)">
                                            <i class="bi bi-check-all me-1"></i> Hepsini Seç
                                        </button>
                                    </div>
                                    <div class="row g-2 mb-4">
                                        @php
                                            $modules = [
                                                'ozet'      => ['Genel Özet',  'bi-bar-chart'],
                                                'urunler'   => ['Ürünler',     'bi-box-seam'],
                                                'fisler'    => ['Fişler',      'bi-receipt'],
                                                'faturalar' => ['Faturalar',   'bi-file-earmark-ruled'],
                                                'cariler'   => ['Cariler',     'bi-people'],
                                                'masalar'   => ['Masalar',     'bi-grid-3x3-gap'],
                                                'raporlar'  => ['Raporlar',    'bi-graph-up'],
                                                'yedekler'  => ['Yedekler',    'bi-cloud-arrow-down'],
                                            ];
                                        @endphp
                                        @foreach($modules as $key => $info)
                                            <div class="col-md-4 col-sm-6">
                                                <label class="d-flex align-items-center p-3 border rounded-3 module-card" style="cursor:pointer;transition:all 0.2s;">
                                                    <input class="form-check-input m-0 me-2 module-check" type="checkbox"
                                                        name="izin_{{ $key }}"
                                                        {{ !empty($izinlerMevcut[$key]) || !$tekPatron ? 'checked' : '' }}>
                                                    <i class="bi {{ $info[1] }} text-primary me-2"></i>
                                                    <span class="fw-medium">{{ $info[0] }}</span>
                                                </label>
                                            </div>
                                        @endforeach
                                    </div>

                                    {{-- BUTONLAR --}}
                                    <div class="d-flex justify-content-end mt-4 pt-3 border-top gap-2">
                                        @if($tekPatron)
                                            <button type="button" class="btn btn-outline-danger px-3"
                                                onclick="if(confirm('Patron hesabı silinecek. Emin misiniz?')) document.getElementById('delPatronForm').submit()">
                                                <i class="bi bi-trash me-1"></i> Sil
                                            </button>
                                        @endif
                                        <button type="submit" class="btn btn-warning fw-bold px-5 shadow-sm"
                                            style="background:#f59e0b;border-color:#f59e0b;color:#fff;">
                                            <i class="bi bi-{{ $tekPatron ? 'check2-circle' : 'plus-circle' }} me-1"></i>
                                            {{ $tekPatron ? 'Hesabı Güncelle' : 'Patron Hesabı Oluştur' }}
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </form>

                        @if($tekPatron ?? false)
                            <form id="delPatronForm" method="POST" 
                                action="{{ route('patron.delete', $tekPatron->id) }}" style="display:none;">
                                @csrf
                            </form>
                        @endif
                    @endif
                </div>
                    </div>
                </div>

{{-- TAB: Yedekler --}}
                <div class="tab-pane fade p-5" id="sync-yedekler" role="tabpanel">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h5 class="mb-0"><i class="bi bi-cloud-arrow-down me-2 text-danger"></i>Bulut Yedekleri</h5>
                        <small class="text-muted">Toplam: <strong>{{ $syncYedekler->count() }}</strong> yedek</small>
                    </div>

                    @php
                        $toplamBoyut  = $syncYedekler->sum('boyut');
                        $manuelSayi   = $syncYedekler->where('tip', 'manual')->count();
                        $otomatikSayi = $syncYedekler->whereIn('tip', ['otomatik', 'zamanlanmis'])->count();
                        $fmt = fn($b) => $b >= 1073741824 ? round($b/1073741824,2).' GB'
                                       : ($b >= 1048576 ? round($b/1048576,2).' MB'
                                       : ($b >= 1024 ? round($b/1024,2).' KB' : $b.' B'));
                    @endphp

                    <div class="row g-3 mb-4">
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Toplam Yedek</div>
                                <h4 class="fw-bold mb-0 mt-1">{{ $syncYedekler->count() }}</h4>
                            </div>
                        </div>
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Toplam Boyut</div>
                                <h4 class="fw-bold mb-0 mt-1 text-danger">{{ $fmt($toplamBoyut) }}</h4>
                            </div>
                        </div>
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Manuel</div>
                                <h4 class="fw-bold mb-0 mt-1">{{ $manuelSayi }}</h4>
                            </div>
                        </div>
                        <div class="col-md-3 col-6">
                            <div class="border rounded-3 p-3 bg-white">
                                <div class="text-muted text-uppercase fw-bold" style="font-size:0.7rem;">Otomatik</div>
                                <h4 class="fw-bold mb-0 mt-1">{{ $otomatikSayi }}</h4>
                            </div>
                        </div>
                    </div>

                    <div class="table-responsive">
                        <table class="table table-hover align-middle">
                            <thead class="table-light">
                                <tr>
                                    <th>#</th>
                                    <th>Dosya Adı</th>
                                    <th>Tip</th>
                                    <th>Boyut</th>
                                    <th>Açıklama</th>
                                    <th>Tarih</th>
                                    <th class="text-center">İndir</th>
                                </tr>
                            </thead>
                            <tbody>
                                @forelse($syncYedekler as $y)
                                    <tr>
                                        <td><small class="text-muted">#{{ $y->id }}</small></td>
                                        <td><code style="font-size:0.75rem;">{{ $y->dosya_adi ?: '—' }}</code></td>
                                        <td>
                                            @if($y->tip === 'manual')
                                                <span class="badge bg-primary bg-opacity-10 text-primary border border-primary">
                                                    <i class="bi bi-hand-index me-1"></i>Manuel
                                                </span>
                                            @elseif($y->tip === 'otomatik')
                                                <span class="badge bg-success bg-opacity-10 text-success border border-success">
                                                    <i class="bi bi-arrow-repeat me-1"></i>Otomatik
                                                </span>
                                            @elseif($y->tip === 'zamanlanmis')
                                                <span class="badge bg-warning bg-opacity-10 text-warning border border-warning">
                                                    <i class="bi bi-clock me-1"></i>Zamanlanmış
                                                </span>
                                            @else
                                                <span class="badge bg-secondary">{{ $y->tip }}</span>
                                            @endif
                                        </td>
                                        <td>{{ $fmt($y->boyut ?? 0) }}</td>
                                        <td><small class="text-muted">{{ $y->aciklama ?: '—' }}</small></td>
                                        <td>{{ $y->yedek_tarihi?->format('d.m.Y H:i') }}</td>
<td class="text-center">
                                        <div class="d-flex gap-1 justify-content-center">
                                            @if($y->url)
                                                <a href="{{ $y->url }}" target="_blank" class="btn btn-sm btn-outline-primary">
                                                    <i class="bi bi-cloud-download"></i>
                                                </a>
                                            @endif
                                            <a href="{{ route('yedek.download', $y->id) }}" class="btn btn-sm btn-outline-success">
                                                <i class="bi bi-download"></i>
                                            </a>
                                            <form method="POST" action="{{ route('yedek.delete', $y->id) }}" style="display:inline;" onsubmit="return confirm('Bu yedeği silmek istediğinize emin misiniz?')">
                                                @csrf
                                                @method('DELETE')
                                                <button type="submit" class="btn btn-sm btn-outline-danger">
                                                    <i class="bi bi-trash"></i>
                                                </button>
                                            </form>
                                        </div>
                                    </td>
                                    </tr>
                                @empty
                                    <tr>
                                        <td colspan="7" class="text-center text-muted py-5">
                                            <i class="bi bi-cloud-slash" style="font-size: 32px; opacity: 0.4;"></i><br>
                                            Bu müşteriden henüz yedek verisi gelmedi.
                                        </td>
                                    </tr>
                                @endforelse
                            </tbody>
                        </table>
                    </div>
                </div>

                <div class="tab-pane fade p-5" id="sozlesmeler" role="tabpanel">
                    <div class="d-flex justify-content-between align-items-center mb-4">
                        <div>
                            <h5 class="mb-1">Genel sözleşme</h5>
                            <p class="text-muted mb-0 small">Satış, bakım ve servis tek metinde. Müşteri ve aktif lisans/paket bilgisi doldurulur.</p>
                        </div>
                        <a class="btn btn-dark px-4" target="_blank" href="{{ route('CustomerAgreement', $Musteri->id) }}">
                            <i class="bi bi-printer me-1"></i> Genel sözleşmeyi aç
                        </a>
                    </div>
                    <div class="border rounded-3 p-4 bg-light">
                        <div class="row g-3 small">
                            <div class="col-md-6"><span class="text-muted">Müşteri</span><div class="fw-semibold">{{ $Musteri->Unvan }}</div></div>
                            <div class="col-md-6"><span class="text-muted">VKN</span><div class="fw-semibold">{{ $Musteri->VergiNo ?: '—' }}</div></div>
                            <div class="col-md-6"><span class="text-muted">Bayi</span><div class="fw-semibold">{{ optional($Musteri->kimbubayi)->Unvan ?: '—' }}</div></div>
                            <div class="col-md-6"><span class="text-muted">Yetkili</span><div class="fw-semibold">{{ $Musteri->Yetkili ?: '—' }}</div></div>
                        </div>
                    </div>
                </div>
                
                <style>
                    .sube-card:hover { border-color: #4f46e5 !important; background-color: #f5f3ff; }
                    .sube-card:has(input:checked) { border-color: #10b981 !important; background-color: #f0fdf4; }
                    .module-card:hover { border-color: #4f46e5 !important; background-color: #f5f3ff; }
                    .module-card:has(input:checked) { border-color: #10b981 !important; background-color: #f0fdf4; }
                </style>

                {{-- Hover ve seçili stiller --}}
                <style>
                    .module-card:hover {
                        border-color: #4f46e5 !important;
                        background-color: #f5f3ff;
                    }
                    .module-card:has(input:checked) {
                        border-color: #10b981 !important;
                        background-color: #f0fdf4;
                    }
                </style>

                <script>
                    // Hepsini Seç / Bırak toggle
                    function toggleAllPermissions(btn) {
                        const card = btn.closest('.card-body');
                        const checks = card.querySelectorAll('.module-check');
                        const allChecked = Array.from(checks).every(c => c.checked);
                        checks.forEach(c => c.checked = !allChecked);
                        btn.innerHTML = allChecked 
                            ? '<i class="bi bi-check-all me-1"></i> Hepsini Seç' 
                            : '<i class="bi bi-x-lg me-1"></i> Hepsini Bırak';
                    }
                </script>

{{-- MODAL: Patron Düzenle (her patron için) --}}
                @foreach($patronlar as $p)
                    @php $izinler = json_decode($p->izinler ?? '{}', true) ?: []; @endphp
                    <div class="modal fade corporate-modal" id="editPatronModal-{{ $p->id }}" tabindex="-1" aria-hidden="true">
                        <div class="modal-dialog modal-dialog-centered">
                            <div class="modal-content">
                                <form method="POST" action="{{ route('patron.update', $p->id) }}">
                                    @csrf
                                    <div class="modal-header">
                                        <h5 class="modal-title fw-bold">
                                            <i class="bi bi-pencil-square me-2 text-primary"></i>Patronu Düzenle
                                        </h5>
                                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                    </div>
                                    <div class="modal-body p-4">
                                        <div class="row g-2">
                                            <div class="col-md-6">
                                                <div class="mb-3">
                                                    <label class="form-label fw-medium">Kullanıcı Adı</label>
                                                    <input type="text" name="kullanici_adi" class="form-control" value="{{ $p->kullanici_adi }}" required>
                                                </div>
                                            </div>
                                            <div class="col-md-6">
                                                <div class="mb-3">
                                                    <label class="form-label fw-medium">Ad Soyad</label>
                                                    <input type="text" name="ad_soyad" class="form-control" value="{{ $p->ad_soyad }}">
                                                </div>
                                            </div>
                                        </div>

                                        <div class="mb-3">
                                            <label class="form-label fw-medium">Yeni Şifre (boş bırakırsan değişmez)</label>
                                            <input type="text" name="sifre" class="form-control" placeholder="Boş bırak = şifre korunur">
                                        </div>

                                        <div class="form-check form-switch mb-3">
                                            <input class="form-check-input" type="checkbox" name="aktif" id="aktif_{{ $p->id }}" {{ $p->aktif ? 'checked' : '' }}>
                                            <label class="form-check-label fw-medium" for="aktif_{{ $p->id }}">Aktif</label>
                                        </div>

                                        <h6 class="text-uppercase text-muted fw-bold fs-8 mb-3">Yetkiler</h6>
                                        <div class="row g-2">
                                            <div class="col-md-6">
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_ozet" id="e_ozet_{{ $p->id }}" {{ !empty($izinler['ozet']) ? 'checked' : '' }}><label class="form-check-label" for="e_ozet_{{ $p->id }}">Özet</label></div>
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_urunler" id="e_urunler_{{ $p->id }}" {{ !empty($izinler['urunler']) ? 'checked' : '' }}><label class="form-check-label" for="e_urunler_{{ $p->id }}">Ürünler</label></div>
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_fisler" id="e_fisler_{{ $p->id }}" {{ !empty($izinler['fisler']) ? 'checked' : '' }}><label class="form-check-label" for="e_fisler_{{ $p->id }}">Fişler</label></div>
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_faturalar" id="e_faturalar_{{ $p->id }}" {{ !empty($izinler['faturalar']) ? 'checked' : '' }}><label class="form-check-label" for="e_faturalar_{{ $p->id }}">Faturalar</label></div>
                                            </div>
                                            <div class="col-md-6">
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_cariler" id="e_cariler_{{ $p->id }}" {{ !empty($izinler['cariler']) ? 'checked' : '' }}><label class="form-check-label" for="e_cariler_{{ $p->id }}">Cariler</label></div>
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_masalar" id="e_masalar_{{ $p->id }}" {{ !empty($izinler['masalar']) ? 'checked' : '' }}><label class="form-check-label" for="e_masalar_{{ $p->id }}">Masalar</label></div>
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_raporlar" id="e_raporlar_{{ $p->id }}" {{ !empty($izinler['raporlar']) ? 'checked' : '' }}><label class="form-check-label" for="e_raporlar_{{ $p->id }}">Raporlar</label></div>
                                                <div class="form-check"><input class="form-check-input" type="checkbox" name="izin_yedekler" id="e_yedekler_{{ $p->id }}" {{ !empty($izinler['yedekler']) ? 'checked' : '' }}><label class="form-check-label" for="e_yedekler_{{ $p->id }}">Yedekler</label></div>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="modal-footer">
                                        <button type="button" class="btn btn-light" data-bs-dismiss="modal">İptal</button>
                                        <button type="submit" class="btn btn-primary px-4">
                                            <i class="bi bi-check2 me-1"></i> Kaydet
                                        </button>
                                    </div>
                                </form>
                            </div>
                        </div>
                    </div>
                @endforeach


                {{-- Bootstrap tooltip init --}}
                <script>
                    document.addEventListener('DOMContentLoaded', () => {
                        document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => new bootstrap.Tooltip(el));
                    });
                </script>

            </div>
        </div>
    </div>
</div>

<div class="toast-container-custom" id="toastContainer"></div>

{{-- Genel Onay Modalı --}}
<div class="modal fade" id="confirmModal" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered modal-sm">
        <div class="modal-content border-0 shadow-lg text-center overflow-hidden" style="border-radius: 20px;">
            <div class="modal-body p-4">
                <div class="mb-3">
                    <div class="bg-warning bg-opacity-10 rounded-circle d-inline-flex p-3">
                        <i class="bi bi-exclamation-lg text-warning display-6"></i>
                    </div>
                </div>
                <h5 class="fw-bold mb-2 text-dark">Emin misiniz?</h5>
                <p class="text-muted small mb-4" id="confirmText">Bu işlemi gerçekleştirmek istediğinize emin misiniz?</p>
                <div class="d-grid gap-2">
                    <button type="button" class="btn btn-primary fw-bold" id="confirmBtnYes">Evet, Onaylıyorum</button>
                    <button type="button" class="btn btn-light text-muted" data-bs-dismiss="modal">Vazgeç</button>
                </div>
            </div>
        </div>
    </div>
</div>

<script>
    // --- 1. TOAST NOTIFICATION SİSTEMİ ---
    function showToast(message, type = 'success') {
        const container = document.getElementById('toastContainer');
        const toast = document.createElement('div');
        toast.className = `custom-toast ${type}`;
        
        let iconHtml = type === 'success' 
            ? '<i class="bi bi-check-circle-fill text-success fs-4 me-3"></i>' 
            : '<i class="bi bi-x-circle-fill text-danger fs-4 me-3"></i>';

        toast.innerHTML = `
            ${iconHtml}
            <div>
                <h6 class="fw-bold mb-0 text-dark">${type === 'success' ? 'Başarılı' : 'Hata'}</h6>
                <small class="text-muted">${message}</small>
            </div>
        `;

        container.appendChild(toast);

        setTimeout(() => {
            toast.style.animation = 'fadeOutRight 0.4s ease-in forwards';
            setTimeout(() => toast.remove(), 400);
        }, 4000);
    }

    // --- 2. ONAY MODALI FONKSİYONU ---
    let confirmCallback = null;
    let confirmModal;

    document.addEventListener('DOMContentLoaded', function() {
        confirmModal = new bootstrap.Modal(document.getElementById('confirmModal'));
        
        document.getElementById('confirmBtnYes').addEventListener('click', function() {
            if (confirmCallback) confirmCallback();
            confirmModal.hide();
        });
    });

    function confirmAction(url, text) {
        document.getElementById('confirmText').innerText = text;
        confirmCallback = () => { window.location.href = url; };
        confirmModal.show();
    }

    // --- 3. PANODAN KOPYALAMA ---
    function copyToClipboard(text) {
        navigator.clipboard.writeText(text).then(() => {
            showToast('Lisans anahtarı kopyalandı.', 'success');
        }).catch(err => console.error('Hata:', err));
    }

    // --- 4. DİNAMİK INPUT GÜNCELLEME (MODAL İÇİ) ---
    function updateSerialInputsForModal(input, containerId) {
        const container = document.getElementById('serialList-' + containerId);
        if (!container) return;

        const newCount = parseInt(input.value) || 1;
        const currentInputs = container.querySelectorAll('.input-group');
        const currentCount = currentInputs.length;
        
        // Paket adını input name'den çek (name="adetler[PaketAdi]")
        const match = input.name.match(/\[(.*?)\]/);
        const packetName = match ? match[1] : '';

        // Artırma
        if (newCount > currentCount) {
            for (let i = currentCount; i < newCount; i++) {
                const div = document.createElement('div');
                div.className = 'input-group input-group-sm';
                div.innerHTML = `
                    <span class="input-group-text bg-white text-muted border-end-0">${i + 1}. Cihaz</span>
                    <input type="text" 
                           class="form-control bg-white serial-input-update" 
                           name="serials[${packetName}][]"
                           placeholder="Seri Numarası Giriniz">
                `;
                container.appendChild(div);
            }
        } 
        // Azaltma
        else if (newCount < currentCount) {
            for (let i = currentCount - 1; i >= newCount; i--) {
                currentInputs[i].remove();
            }
        }
    }

    // --- 5. FORM SUBMIT (GÜNCELLEME) ---
    function prepareAndSubmitForm(siparisNo) {
        const modalElement = document.getElementById('detailModal-' + siparisNo);
        const form = document.getElementById('bulkUpdateForm-' + siparisNo);
        
        if (!modalElement || !form) return;

        // Eski hidden inputları temizle
        form.querySelectorAll('.dynamic-input').forEach(i => i.remove());

        const checkboxes = modalElement.querySelectorAll('input[type="checkbox"][name="paketler[]"]');
        let serialError = false;

        checkboxes.forEach((checkbox, index) => {
            const row = checkbox.closest('tr'); 
            if(!row) return;

            const dateInput = row.querySelector('input[type="date"]');
            const adetInput = row.querySelector('input.paket-adet-update');
            const paketName = checkbox.value; 
            
            // Seri Numaralarını Topla
            let collectedSerials = [];
            if (checkbox.checked && checkbox.getAttribute('data-paket-tipi') === 'yazarkasa') {
                const targetId = checkbox.getAttribute('data-bs-target').replace('#', '');
                const collapseDiv = document.getElementById(targetId);
                
                if (collapseDiv) {
                    const serialInputs = collapseDiv.querySelectorAll('.serial-input-update');
                    serialInputs.forEach(inp => {
                        const val = inp.value.trim();
                        // Boş bırakılabilir mi? Genelde zorunlu olması iyidir.
                        if (!val) serialError = true;
                        collectedSerials.push(val);
                    });
                }
            }

            // Hidden input oluşturma
            const inputs = [
                {name: `paketler[${index}][paketName]`, value: paketName},
                {name: `paketler[${index}][status]`, value: checkbox.checked ? 1 : 0},
                {name: `paketler[${index}][date]`, value: dateInput ? dateInput.value : ''},
                {name: `paketler[${index}][adet]`, value: adetInput ? (parseInt(adetInput.value) || 0) : 0}
            ];

            // Serileri inputlara ekle
            collectedSerials.forEach(serial => {
                inputs.push({name: `paketler[${index}][serials][]`, value: serial});
            });

            inputs.forEach(data => {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = data.name;
                input.value = data.value;
                input.className = 'dynamic-input';
                form.appendChild(input);
            });
        });

        const pcNameInput = modalElement.querySelector('.pc-name-input');
        const pcName = pcNameInput ? pcNameInput.value.trim() : '';
        if (!pcName) {
            showToast('PC adı boş bırakılamaz.', 'warning');
            if (pcNameInput) pcNameInput.focus();
            return;
        }

        if (serialError) {
            showToast('Lütfen aktif edilen yazarkasalar için seri numaralarını eksiksiz giriniz.', 'warning');
            return;
        }

        const pcHidden = document.createElement('input');
        pcHidden.type = 'hidden';
        pcHidden.name = 'pc_name';
        pcHidden.value = pcName;
        pcHidden.className = 'dynamic-input';
        form.appendChild(pcHidden);

        // Native Onay Modalı
        document.getElementById('confirmText').innerText = 'PC adı ve seçili paketler güncellenecek. Varsa fark bakiyeden düşülecek.';
        confirmCallback = () => { form.submit(); };
        confirmModal.show();
    }

    // --- 6. CHECKBOX & INPUT LOGIC ---
    document.addEventListener('change', function(e) {
        // Sadece modal içindeki checkboxlar için
        if (e.target && e.target.classList.contains('update-checkbox')) {
            const checkbox = e.target;
            const row = checkbox.closest('tr');
            const modal = checkbox.closest('.modal-content');
            
            const dateInput = row.querySelector('input[type="date"]');
            const adetInput = row.querySelector('input.paket-adet-update');
            const isEnabled = checkbox.checked;

            if(dateInput) dateInput.disabled = !isEnabled;
            if(adetInput) adetInput.disabled = !isEnabled;

            // Yazarkasa Collapse Inputları
            if (checkbox.getAttribute('data-paket-tipi') === 'yazarkasa') {
                const targetId = checkbox.getAttribute('data-bs-target').replace('#', '');
                const collapseDiv = document.getElementById(targetId);
                
                if (collapseDiv) {
                    const serialInputs = collapseDiv.querySelectorAll('input');
                    // Checkbox kapandığında inputları disable et
                    serialInputs.forEach(inp => inp.disabled = !isEnabled);
                }
            }

            // Ana Paket Mantığı (Tekil Seçim - Radyo gibi davranması için)
            if (isEnabled && checkbox.getAttribute('data-paket-tipi') === 'paket') {
                modal.querySelectorAll('input[name="paketler[]"][data-paket-tipi="paket"]').forEach(other => {
                    if (other !== checkbox && other.checked) {
                        other.checked = false;
                        other.dispatchEvent(new Event('change')); // Diğerinin kapanış logicini tetikle
                    }
                });
            }
            
            // Tarih Otomatik Doldurma
            if (isEnabled && dateInput && !dateInput.value) {
                const today = new Date();
                const nextYear = new Date(today);
                nextYear.setFullYear(today.getFullYear() + 1);
                dateInput.value = nextYear.toISOString().split('T')[0];
            }
        }
    });

    // --- 7. BACKEND MESAJLARI (Sayfa Yüklendiğinde) ---
    document.addEventListener('DOMContentLoaded', function () {
        @if(session('success'))
            showToast("{{ session('success') }}", 'success');
        @endif

        @if(session('error'))
            showToast("{{ session('error') }}", 'error');
        @endif
        
        @if(session('warning'))
            showToast("{{ session('warning') }}", 'warning');
        @endif
    });
</script>
@endsection