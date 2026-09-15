@extends('layouts.app')

@section('content')

<style>
    :root {
        --primary-red: #1a237e;
        --soft-red: #fef2f2;
        --border-color: #f1f5f9;
    }

    .price-list-container {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .price-item {
        background: #fff;
        border: 1px solid var(--border-color);
        border-radius: 12px;
        padding: 1.25rem;
        display: flex;
        align-items: center;
        justify-content: space-between;
        transition: all 0.2s ease;
        position: relative;
        overflow: hidden;
    }

    .price-item.js-admin-price {
        cursor: pointer;
    }

    .price-item.js-admin-price:focus {
        outline: 2px solid #7379c5;
        outline-offset: 2px;
    }

    .item-left {
        display: flex;
        align-items: center;
        gap: 1.25rem;
        flex: 1;
    }

    .item-icon {
        width: 50px;
        height: 50px;
        background-color: var(--soft-red);
        color: var(--primary-red);
        border-radius: 10px;
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
    }

    .item-info h6 {
        font-weight: 700;
        color: #1e293b;
        margin-bottom: 0.25rem;
        font-size: 1rem;
    }

    .item-info p {
        color: #64748b;
        font-size: 0.85rem;
        margin-bottom: 0;
        line-height: 1.4;
    }

    .item-price-wrapper {
        display: flex;
        align-items: center;
        gap: 1.5rem;
    }

    .item-price {
        text-align: right;
        min-width: 120px;
    }

    .price-tag {
        font-family: 'Inter', sans-serif;
        font-size: 1.25rem;
        font-weight: 800;
        color: var(--primary-red);
        display: block;
    }

    .price-label {
        font-size: 0.7rem;
        color: #94a3b8;
        text-transform: uppercase;
        font-weight: 600;
        letter-spacing: 0.5px;
    }

    .category-header {
        display: flex;
        align-items: center;
        padding-bottom: 0.5rem;
        border-bottom: 2px solid #e2e8f0;
        margin-bottom: 1rem;
        margin-top: 2rem;
    }

    .category-header h5 {
        color: #334155;
        font-weight: 700;
        margin: 0;
        text-transform: uppercase;
        font-size: 0.9rem;
        letter-spacing: 0.5px;
    }
</style>

<div class="main-content pt-4">

    <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
            <h4 class="fw-bold text-dark mb-1">
                <i class="material-icons-outlined align-middle text-danger me-2">format_list_bulleted</i>
                Paket Fiyat Listesi
            </h4>
            <p class="text-muted fs-7 mb-0">
                Belirlediğiniz özel satış fiyatları müşterilerinize teklif oluştururken otomatik yansıyacaktır.
            </p>
        </div>
    </div>

    @php
        $veriHavuzu = $lisanspaket ?? $LisansPaket ?? \App\Models\LisansPaketModel::all();
        $koleksiyon = collect($veriHavuzu);
    @endphp

    @foreach ([
        'paket' => ['title' => 'Ana Yazılım Paketleri', 'icon' => 'inventory_2'],
        'yazarkasa' => ['title' => 'Yazar Kasa Entegrasyonları', 'icon' => 'point_of_sale'],
        'modul' => ['title' => 'Ek Modüller', 'icon' => 'extension'],
        'uygulama' => ['title' => 'Mobil Uygulamalar', 'icon' => 'smartphone']
    ] as $type => $config)

        @php
            $currentPakets = $koleksiyon->where('PaketTipi', $type);
        @endphp

        @if($currentPakets->count() > 0)
            <div class="mb-4">

                <div class="category-header">
                    <h5>{{ $config['title'] }}</h5>
                </div>

                <div class="price-list-container">
                    @foreach ($currentPakets as $paket)
                        @php
                            $guncelOzelFiyat = \App\Models\BayiOzelFiyatModel::where('user_id', Auth::id())
                                ->where('paket_id', $paket->id)
                                ->first();

                            $bayiSatisFiyati = null;

                            if (
                                $guncelOzelFiyat &&
                                $guncelOzelFiyat->satis_fiyati !== null &&
                                $guncelOzelFiyat->satis_fiyati !== '' &&
                                floatval($guncelOzelFiyat->satis_fiyati) > 0
                            ) {
                                $bayiSatisFiyati = floatval($guncelOzelFiyat->satis_fiyati);
                            }

                            // Admin paket fiyat listesinden güncellenen sistem fiyatı
                            $admininFiyati = floatval($paket->PaketFiyati ?? $paket->paket_fiyati ?? 0);

                            // Bayi özel fiyat girdiyse onu göster, boşsa sistem fiyatına bağlı kal
                            $gosterilecekFiyat = ($bayiSatisFiyati !== null)
                                ? $bayiSatisFiyati
                                : $admininFiyati;
                        @endphp

                        <div class="price-item @if(Auth::user()->role === 'admin') js-admin-price @endif"
                             @if(Auth::user()->role === 'admin')
                             role="button"
                             tabindex="0"
                             data-id="{{ $paket->id }}"
                             data-name="{{ $paket->PaketAdi }}"
                             data-price="{{ $admininFiyati }}"
                             @endif>

                            <div class="item-left">
                                <div class="item-icon">
                                    <i class="material-icons-outlined fs-4">{{ $config['icon'] }}</i>
                                </div>
                                <div class="item-info">
                                    <h6>{{ $paket->PaketAdi }}</h6>
                                    <p>{{ $paket->PaketAciklama ?? 'Detaylı açıklama bulunmuyor.' }}</p>
                                </div>
                            </div>

                            <div class="item-price-wrapper">
                                <div class="item-price">
                                    <span class="price-label">
                                        @if($bayiSatisFiyati === null)
                                            <small class="text-success d-block fw-bold">(SİSTEM FİYATI AKTİF)</small>
                                        @else
                                            <small class="text-primary d-block fw-bold">(ÖZEL FİYATINIZ)</small>
                                        @endif
                                        Müşteri Satış Fiyatı
                                    </span>

                                    <span class="price-tag" id="price-tag-{{ $paket->id }}">
                                        {{ number_format($gosterilecekFiyat, 2, ',', '.') }} ₺
                                    </span>
                                </div>

                                <div>
                                    @if(Auth::user()->role != 'admin')
    <button type="button"
            class="btn btn-sm btn-outline-primary d-flex align-items-center px-3"
            onclick="event.stopPropagation(); openPriceModal('{{ $paket->id }}', '{{ addslashes($paket->PaketAdi) }}', '{{ $bayiSatisFiyati ?? '' }}')">
        <i class="material-icons-outlined fs-6 me-1">edit</i> Düzenle
    </button>
@endif
                                </div>
                            </div>

                        </div>
                    @endforeach
                </div>
            </div>
        @endif
    @endforeach
</div>

<div class="modal fade" id="sistemFiyatModal" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered modal-sm">
        <div class="modal-content border-0 shadow">
            <div class="modal-header py-3">
                <h6 class="modal-title fw-bold mb-0" id="sistemModalBaslik">Fiyat</h6>
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Kapat"></button>
            </div>
            <div class="modal-body pt-2 pb-3">
                <input type="hidden" id="sistemPaketId">
                <p class="small text-muted mb-2">Mevcut: <span id="sistemMevcutFiyat" class="fw-semibold text-dark"></span></p>
                <label class="form-label fw-bold small mb-1" for="sistemYeniFiyat">Yeni fiyat</label>
                <div class="input-group">
                    <input type="text" inputmode="decimal" class="form-control text-end fw-bold" id="sistemYeniFiyat" autocomplete="off">
                    <span class="input-group-text">₺</span>
                </div>
                <div class="text-danger small mt-2 d-none" id="sistemFiyatHata"></div>
            </div>
            <div class="modal-footer py-2">
                <button type="button" class="btn btn-light" data-bs-dismiss="modal">Vazgeç</button>
                <button type="button" class="btn btn-primary" id="sistemFiyatKaydet">Kaydet</button>
            </div>
        </div>
    </div>
</div>

<div class="modal fade" id="fiyatGuncelleModal" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered modal-sm">
        <div class="modal-content border-0 shadow">
            <div class="modal-header bg-light py-3">
                <h6 class="modal-title fw-bold text-dark" id="modalPaketAdi">Paket Fiyatı Düzenle</h6>
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>

            <div class="modal-body p-4">
                <input type="hidden" id="modalPaketId">

                <div class="mb-3">
                    <label class="form-label fw-bold text-dark mb-2">Yeni Satış Fiyatı</label>
                    <div class="input-group">
                        <input type="number"
                               step="0.01"
                               min="0"
                               class="form-control text-end fw-bold"
                               id="modalSatisFiyati"
                               placeholder="Boş bırakırsan sistem fiyatına bağlanır">
                        <span class="input-group-text bg-white text-muted">₺</span>
                    </div>
                    <small class="text-muted d-block mt-2">
                        Boş kaydedersen fiyat tekrar sistem/admin fiyatına bağlanır.
                    </small>
                </div>

                <button type="button" class="btn btn-primary w-100 fw-bold py-2" onclick="saveDealerPrice()">
                    <i class="material-icons-outlined align-middle fs-6 me-1">save</i> Fiyatı Kaydet
                </button>
            </div>
        </div>
    </div>
</div>

<script>
    let priceModal;
    let sistemModal;

    document.addEventListener('DOMContentLoaded', function() {
        const dealerEl = document.getElementById('fiyatGuncelleModal');
        if (dealerEl) priceModal = new bootstrap.Modal(dealerEl);
        const sistemEl = document.getElementById('sistemFiyatModal');
        if (sistemEl) sistemModal = new bootstrap.Modal(sistemEl);

        document.querySelectorAll('.js-admin-price').forEach(function (row) {
            row.addEventListener('click', function () {
                openSistemFiyat(row);
            });
            row.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    openSistemFiyat(row);
                }
            });
        });

        const kaydetBtn = document.getElementById('sistemFiyatKaydet');
        if (kaydetBtn) kaydetBtn.addEventListener('click', saveSistemFiyat);

        const input = document.getElementById('sistemYeniFiyat');
        if (input) {
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    saveSistemFiyat();
                }
            });
        }
    });

    function formatTr(n) {
        return Number(n).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ₺';
    }

    function openSistemFiyat(row) {
        const id = row.getAttribute('data-id');
        const ad = row.getAttribute('data-name');
        const fiyat = row.getAttribute('data-price');
        document.getElementById('sistemPaketId').value = id;
        document.getElementById('sistemModalBaslik').textContent = ad;
        document.getElementById('sistemMevcutFiyat').textContent = formatTr(fiyat);
        document.getElementById('sistemYeniFiyat').value = '';
        document.getElementById('sistemFiyatHata').classList.add('d-none');
        sistemModal.show();
        setTimeout(function () { document.getElementById('sistemYeniFiyat').focus(); }, 250);
    }

    function saveSistemFiyat() {
        const id = document.getElementById('sistemPaketId').value;
        const fiyat = document.getElementById('sistemYeniFiyat').value.trim();
        const hata = document.getElementById('sistemFiyatHata');
        hata.classList.add('d-none');

        if (fiyat === '') {
            hata.textContent = 'Yeni fiyat girin.';
            hata.classList.remove('d-none');
            return;
        }

        const kaydetBtn = document.getElementById('sistemFiyatKaydet');
        kaydetBtn.disabled = true;

        fetch('{{ url('/fiyat-listesi') }}/' + id, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').getAttribute('content')
            },
            body: JSON.stringify({ fiyat: fiyat })
        })
        .then(function (response) { return response.json().then(function (data) { return { ok: response.ok, data: data }; }); })
        .then(function (res) {
            kaydetBtn.disabled = false;
            if (!res.ok || !res.data.success) {
                hata.textContent = (res.data && res.data.message) ? res.data.message : 'Kayıt başarısız.';
                hata.classList.remove('d-none');
                return;
            }
            const tag = document.getElementById('price-tag-' + id);
            if (tag) tag.textContent = res.data.fiyat_text;
            const row = document.querySelector('.js-admin-price[data-id="' + id + '"]');
            if (row) row.setAttribute('data-price', res.data.fiyat);
            sistemModal.hide();
        })
        .catch(function () {
            kaydetBtn.disabled = false;
            hata.textContent = 'Sunucu hatası, tekrar deneyin.';
            hata.classList.remove('d-none');
        });
    }

    function openPriceModal(id, ad, mevcutFiyat) {
        document.getElementById('modalPaketId').value = id;
        document.getElementById('modalPaketAdi').textContent = ad + ' - Satış Fiyatı';
        document.getElementById('modalSatisFiyati').value = mevcutFiyat ?? '';
        priceModal.show();
    }

    function saveDealerPrice() {
        const id = document.getElementById('modalPaketId').value;
        const fiyat = document.getElementById('modalSatisFiyati').value.trim();

        if (fiyat !== '' && parseFloat(fiyat) < 0) {
            alert('Lütfen geçerli bir fiyat giriniz!');
            return;
        }

        fetch("{{ route('UpdateDealerPrices', Auth::id()) }}", {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': '{{ csrf_token() }}'
            },
            body: JSON.stringify({
                is_dealer_self_update: true,
                prices: {
                    [id]: fiyat
                }
            })
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                location.reload();
            } else {
                alert('Bir hata oluştu: ' + (data.message || 'Bilinmeyen hata'));
            }
        })
        .catch(error => {
            console.error('Hata:', error);
            alert('Sunucu hatası oluştu, lütfen tekrar deneyin.');
        });
    }
</script>

@endsection