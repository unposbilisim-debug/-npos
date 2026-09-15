@extends('layouts.app')

@section('content')

<link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">
<link href="https://fonts.googleapis.com/css?family=Material+Icons+Round" rel="stylesheet">
<script src="https://cdn.jsdelivr.net/npm/apexcharts"></script>

<style>
    :root {
        --brand: #f89d1d;
        --text: #0f172a;
        --muted: #64748b;
        --line: #e2e8f0;
        --card: #ffffff;
        --ok: #059669;
        --warn: #d97706;
        --danger: #dc2626;
        --info: #2563eb;
    }

    .desk-page {
        font-family: 'Plus Jakarta Sans', sans-serif;
        color: var(--text);
    }

    .desk-card {
        background: var(--card);
        border: 1px solid var(--line);
        border-radius: 14px;
        padding: 1.25rem 1.4rem;
        height: 100%;
    }

    .desk-kicker {
        font-size: 0.7rem;
        font-weight: 700;
        letter-spacing: .06em;
        text-transform: uppercase;
        color: var(--muted);
        margin-bottom: .2rem;
    }

    .desk-title {
        font-size: 1.45rem;
        font-weight: 800;
        letter-spacing: -.03em;
        margin: 0;
    }

    .desk-chip {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        border: 1px solid var(--line);
        background: #f8fafc;
        border-radius: 999px;
        padding: .35rem .75rem;
        font-size: .78rem;
        font-weight: 600;
        color: #334155;
        text-decoration: none;
    }
    .desk-chip:hover { border-color: #fdba74; color: #9a3412; background: #fff7ed; }

    .desk-queue-row {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 12px 4px;
        border-bottom: 1px solid #f1f5f9;
        text-decoration: none;
        color: inherit;
    }
    .desk-queue-row:last-child { border-bottom: none; }
    .desk-queue-row:hover { background: #f8fafc; }

    .desk-type {
        font-size: .68rem;
        font-weight: 800;
        letter-spacing: .04em;
        text-transform: uppercase;
        border-radius: 8px;
        padding: .28rem .5rem;
        white-space: nowrap;
    }
    .desk-type-lisans { background: #eff6ff; color: #1d4ed8; }
    .desk-type-yazarkasa { background: #fff7ed; color: #c2410c; }
    .desk-type-teklif { background: #f5f3ff; color: #6d28d9; }

    .stat-quiet {
        border-left: 3px solid var(--line);
        padding-left: 1rem;
    }

    .table-modern { width: 100%; border-collapse: separate; border-spacing: 0; }
    .table-modern th {
        background: #f8fafc;
        color: var(--muted);
        font-size: 0.72rem;
        font-weight: 700;
        text-transform: uppercase;
        padding: 10px 14px;
        border-bottom: 1px solid var(--line);
    }
    .table-modern td {
        padding: 12px 14px;
        border-bottom: 1px solid #f1f5f9;
        font-size: 0.88rem;
        vertical-align: middle;
    }
    .progress-thin { height: 6px; border-radius: 10px; background: #f1f5f9; overflow: hidden; }
    .progress-bar-custom { height: 100%; border-radius: 10px; }
</style>

@php
    $kuyrukGoster = array_slice($kuyruk['ogeler'] ?? [], 0, 12);
    $turEtiket = [
        'lisans' => 'Lisans',
        'yazarkasa' => 'Yazar kasa',
        'teklif' => 'Teklif',
    ];
@endphp

<div class="container-fluid py-4 px-md-4 desk-page">

    <div class="d-flex flex-column flex-lg-row align-items-lg-end justify-content-between gap-3 mb-4">
        <div>
            <div class="desk-kicker">Desk · operasyon</div>
            <h1 class="desk-title">Bugün / yaklaşan</h1>
            <p class="text-muted mb-0 mt-1">90 güne giren lisans ve yazar kasa ile bekleyen teklifler. Saat Türkiye.</p>
        </div>
        <div class="d-flex align-items-center gap-2">
            <div class="desk-chip">
                <span class="material-icons-round" style="font-size:16px">event</span>
                {{ $now->locale('tr')->translatedFormat('d MMMM Y') }}
            </div>
            <div class="desk-chip">
                <span class="material-icons-round" style="font-size:16px">schedule</span>
                <span id="liveClock">{{ $now->format('H:i') }}</span>
            </div>
        </div>
    </div>

    <div class="desk-card mb-4">
        <div class="d-flex flex-column flex-md-row align-items-md-center justify-content-between gap-3 mb-3">
            <div>
                <h2 class="h6 fw-bold mb-1">İş kuyruğu</h2>
                <p class="text-muted small mb-0">{{ $kuyruk['toplam'] }} kayıt · tıklayınca ilgili listeye gider</p>
            </div>
            <div class="d-flex flex-wrap gap-2">
                <a class="desk-chip" href="{{ route('UpcomingLicenses', ['gun' => 90]) }}">
                    Lisans {{ $kuyruk['lisans_sayisi'] }}
                </a>
                <a class="desk-chip" href="{{ route('admin.yazarkasa_report') }}">
                    Yazar kasa {{ $kuyruk['yazarkasa_sayisi'] }}
                </a>
                <a class="desk-chip" href="{{ route('OfferList') }}">
                    Teklif {{ $kuyruk['teklif_sayisi'] }}
                </a>
            </div>
        </div>

        @forelse($kuyrukGoster as $oge)
            <a class="desk-queue-row" href="{{ $oge['url'] }}">
                <span class="desk-type desk-type-{{ $oge['tur'] }}">{{ $turEtiket[$oge['tur']] ?? $oge['tur'] }}</span>
                <div class="flex-grow-1 min-w-0">
                    <div class="fw-semibold text-truncate">{{ $oge['musteri'] }}</div>
                    <div class="small text-muted text-truncate">{{ $oge['baslik'] }}@if(!empty($oge['bayi'])) · {{ $oge['bayi'] }}@endif</div>
                </div>
                <div class="text-end">
                    @if($oge['tur'] === 'teklif')
                        <div class="fw-bold">₺{{ number_format($oge['tutar'] ?? 0, 2, ',', '.') }}</div>
                        <div class="small text-muted">Beklemede</div>
                    @elseif(($oge['kalanGun'] ?? 1) < 0)
                        <span class="badge bg-danger">{{ abs($oge['kalanGun']) }} gün geçti</span>
                    @elseif(($oge['kalanGun'] ?? 99) <= 7)
                        <span class="badge bg-danger">{{ $oge['kalanGun'] }} gün</span>
                    @else
                        <span class="badge text-bg-light border">{{ $oge['kalanGun'] }} gün</span>
                    @endif
                </div>
            </a>
        @empty
            <div class="text-center py-5 text-muted">
                90 gün içinde yaklaşan lisans/yazar kasa yok; bekleyen teklif de yok.
            </div>
        @endforelse

        @if(($kuyruk['toplam'] ?? 0) > 12)
            <div class="pt-2 small text-muted">İlk 12 kayıt gösteriliyor. Chip’lerden tam listeye geçin.</div>
        @endif
    </div>

    <div class="row g-3 mb-2">
        <div class="col-md-4">
            <div class="desk-card stat-quiet" style="border-left-color:#f89d1d">
                <div class="desk-kicker">Bugün</div>
                <div class="fs-3 fw-bold">₺{{ number_format($systemStats['bugun_ciro'], 2, ',', '.') }}</div>
                <div class="small text-muted mt-1">{{ $systemStats['bugun_adet'] }} satış · liste fiyatı</div>
            </div>
        </div>
        <div class="col-md-4">
            <div class="desk-card stat-quiet" style="border-left-color:#334155">
                <div class="desk-kicker">Bu ay</div>
                <div class="fs-3 fw-bold">₺{{ number_format($systemStats['buay_ciro'], 2, ',', '.') }}</div>
                <div class="small text-muted mt-1">{{ $systemStats['buay_adet'] }} satış</div>
            </div>
        </div>
        <div class="col-md-4">
            <div class="desk-card stat-quiet" style="border-left-color:#059669">
                <div class="desk-kicker">Kümülatif</div>
                <div class="fs-3 fw-bold">₺{{ number_format($systemStats['toplam_ciro'], 2, ',', '.') }}</div>
                <div class="small text-muted mt-1">Aktif paket × paket listesi</div>
            </div>
        </div>
    </div>
    <p class="small text-muted mb-4">Ciro: bayi özel fiyatı yansımaz. Aktif lisans sayısı paket satırıdır (aynı siparişte birden fazla paket olabilir).</p>

    <div class="desk-card p-0 overflow-hidden mb-4">
        <div class="row g-0">
            <div class="col-md-3 border-end p-3 text-center">
                <div class="fs-4 fw-bold mb-0">{{ $totalCustomers }}</div>
                <div class="desk-kicker mb-0">Müşteri</div>
            </div>
            <div class="col-md-3 border-end p-3 text-center">
                <div class="fs-4 fw-bold mb-0">{{ $networkStats['aktif_lisans'] }}</div>
                <div class="desk-kicker mb-0">Aktif paket satırı</div>
            </div>
            <div class="col-md-3 border-end p-3 text-center">
                <div class="fs-4 fw-bold mb-0">{{ $totalBayi }}</div>
                <div class="desk-kicker mb-0">Bayi</div>
            </div>
            <div class="col-md-3 p-3 text-center">
                <div class="fs-4 fw-bold mb-0">{{ $systemStats['toplam_adet'] }}</div>
                <div class="desk-kicker mb-0">Sipariş (satış)</div>
            </div>
        </div>
    </div>

    <div class="row g-4 mb-4">
        <div class="col-xl-8">
            <div class="desk-card mb-4">
                <div class="d-flex justify-content-between align-items-center mb-3">
                    <div>
                        <h2 class="h6 fw-bold mb-0">Sistem ciro</h2>
                        <small class="text-muted">Son 6 ay · bin / milyon yazılır, B (milyar) kullanılmaz</small>
                    </div>
                </div>
                <div id="revenueChart" style="height: 280px;"></div>
            </div>

            <div class="desk-card p-0 overflow-hidden">
                <div class="p-3 px-4 border-bottom d-flex align-items-center justify-content-between">
                    <h2 class="h6 fw-bold mb-0">Son satışlar</h2>
                    <span class="small text-muted">Son 10</span>
                </div>
                <div class="table-responsive">
                    <table class="table-modern">
                        <thead>
                            <tr>
                                <th class="ps-4">Müşteri</th>
                                <th>Bayi</th>
                                <th>Paket</th>
                                <th class="text-end pe-4">Tutar</th>
                            </tr>
                        </thead>
                        <tbody>
                            @forelse($sonSatislar as $satis)
                                @php
                                    $toplam = 0;
                                    $paketAdlari = [];
                                    $json = json_decode($satis->Lisans, true) ?? [];
                                    foreach($json as $item) {
                                        if(isset($item['yazarkasa']) && is_array($item['yazarkasa'])) {
                                            foreach($item['yazarkasa'] as $yk) {
                                                if(isset($yk['paketName'])) {
                                                    $paketAdlari[] = $yk['paketName'];
                                                    $toplam += $paketFiyatlari[$yk['paketName']] ?? 0;
                                                }
                                            }
                                        } elseif(isset($item['paketName'])) {
                                            $paketAdlari[] = $item['paketName'];
                                            $toplam += $paketFiyatlari[$item['paketName']] ?? 0;
                                        } elseif(is_array($item)) {
                                            foreach($item as $sub) {
                                                if(is_array($sub) && isset($sub['paketName'])) {
                                                    $paketAdlari[] = $sub['paketName'];
                                                    $toplam += $paketFiyatlari[$sub['paketName']] ?? 0;
                                                }
                                            }
                                        }
                                    }
                                    $created = $satis->created_at?->timezone('Europe/Istanbul');
                                    $dateStr = $created && $created->isToday()
                                        ? 'Bugün '.$created->format('H:i')
                                        : ($created ? $created->format('d.m.Y H:i') : '—');
                                @endphp
                                <tr>
                                    <td class="ps-4">
                                        <div class="fw-semibold">{{ \Illuminate\Support\Str::limit($satis->musterix->Unvan ?? 'Silinmiş', 28) }}</div>
                                        <small class="text-muted">{{ $dateStr }}</small>
                                    </td>
                                    <td>{{ $satis->bayilers->name ?? '—' }}</td>
                                    <td>
                                        {{ \Illuminate\Support\Str::limit($paketAdlari[0] ?? '—', 18) }}
                                        @if(count($paketAdlari)>1) <span class="text-muted">+{{ count($paketAdlari)-1 }}</span> @endif
                                    </td>
                                    <td class="text-end pe-4 fw-semibold">₺{{ number_format($toplam, 2, ',', '.') }}</td>
                                </tr>
                            @empty
                                <tr><td colspan="4" class="text-center py-4 text-muted">Satış yok.</td></tr>
                            @endforelse
                        </tbody>
                    </table>
                </div>
            </div>
        </div>

        <div class="col-xl-4">
            <div class="desk-card">
                <h2 class="h6 fw-bold mb-1">Paket dağılımı</h2>
                <p class="text-muted small mb-3">Aktif paket satırlarının oranı</p>
                @php
                    $colors = ['#f89d1d', '#334155', '#2563eb', '#059669', '#d97706'];
                    $colorIndex = 0;
                    $totalActive = $networkStats['aktif_lisans'] > 0 ? $networkStats['aktif_lisans'] : 1;
                @endphp
                @forelse($networkStats['dagilim'] as $paketName => $count)
                    @php
                        $percent = ($count / $totalActive) * 100;
                        $currentColor = $colors[$colorIndex % count($colors)];
                        $colorIndex++;
                    @endphp
                    <div class="mb-3">
                        <div class="d-flex justify-content-between mb-1">
                            <span class="small fw-semibold">{{ $paketName }}</span>
                            <span class="small">{{ $count }} <span class="text-muted">(%{{ round($percent) }})</span></span>
                        </div>
                        <div class="progress-thin">
                            <div class="progress-bar-custom" style="width: {{ $percent }}%; background-color: {{ $currentColor }};"></div>
                        </div>
                    </div>
                @empty
                    <p class="text-muted small mb-0">Aktif paket yok.</p>
                @endforelse
            </div>
        </div>
    </div>
</div>

<script>
    document.addEventListener("DOMContentLoaded", () => {
        setInterval(() => {
            const el = document.getElementById('liveClock');
            if (el) {
                el.textContent = new Date().toLocaleTimeString('tr-TR', {
                    hour: '2-digit',
                    minute: '2-digit',
                    timeZone: 'Europe/Istanbul'
                });
            }
        }, 1000);

        function formatTlAxis(value) {
            const abs = Math.abs(value);
            if (abs >= 1000000) {
                return '₺' + (value / 1000000).toLocaleString('tr-TR', { maximumFractionDigits: 1 }) + ' Mn';
            }
            if (abs >= 1000) {
                return '₺' + (value / 1000).toLocaleString('tr-TR', { maximumFractionDigits: 0 }) + ' bin';
            }
            return '₺' + Number(value).toLocaleString('tr-TR');
        }

        var options = {
            series: [{ name: 'Toplam ciro', data: {!! json_encode($chartData['data']) !!} }],
            chart: {
                type: 'area',
                height: 280,
                fontFamily: 'Plus Jakarta Sans, sans-serif',
                toolbar: { show: false },
                background: '#fff'
            },
            colors: ['#f89d1d'],
            stroke: { curve: 'smooth', width: 2 },
            fill: { type: 'solid', opacity: 0.12 },
            dataLabels: { enabled: false },
            xaxis: {
                categories: {!! json_encode($chartData['labels']) !!},
                axisBorder: { show: false },
                axisTicks: { show: false },
                labels: { style: { colors: '#64748b' } }
            },
            yaxis: {
                labels: {
                    style: { colors: '#64748b' },
                    formatter: formatTlAxis
                }
            },
            grid: { borderColor: '#e2e8f0', strokeDashArray: 4 },
            tooltip: {
                theme: 'light',
                y: { formatter: function (val) { return '₺' + new Intl.NumberFormat('tr-TR').format(val); } }
            }
        };

        var chart = new ApexCharts(document.querySelector("#revenueChart"), options);
        chart.render();
    });
</script>

@endsection
