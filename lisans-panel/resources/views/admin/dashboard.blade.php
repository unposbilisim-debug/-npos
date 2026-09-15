@extends('layouts.app')

@section('content')

<link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">
<link href="https://fonts.googleapis.com/css?family=Material+Icons+Round" rel="stylesheet">
<script src="https://cdn.jsdelivr.net/npm/apexcharts"></script>

<style>
    .desk-page { font-family: 'Plus Jakarta Sans', sans-serif; color: #0f172a; }
    .desk-page .desk-kicker {
        font-size: .65rem; font-weight: 700; letter-spacing: .06em;
        text-transform: uppercase; color: #64748b; margin: 0;
    }
    .desk-kpi {
        background: #fff; border: 1px solid #e2e8f0; border-radius: 12px;
        overflow: hidden;
    }
    .desk-kpi-item { padding: .7rem 1rem; min-width: 0; }
    .desk-kpi-item .val { font-size: 1.05rem; font-weight: 800; letter-spacing: -.02em; line-height: 1.2; }
    .desk-box {
        background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: .9rem 1rem;
    }
    .desk-finance .val { font-size: 1.15rem; font-weight: 800; }
    .desk-row {
        display: flex; align-items: center; gap: 10px;
        padding: .45rem 0; border-bottom: 1px solid #f1f5f9;
        text-decoration: none; color: inherit; font-size: .85rem;
    }
    .desk-row:last-child { border-bottom: none; }
    .desk-row:hover { color: #9a3412; }
    .desk-type {
        font-size: .62rem; font-weight: 800; text-transform: uppercase;
        border-radius: 6px; padding: .15rem .4rem; white-space: nowrap;
    }
    .desk-type-lisans { background: #eff6ff; color: #1d4ed8; }
    .desk-type-yazarkasa { background: #fff7ed; color: #c2410c; }
    .progress-thin { height: 5px; border-radius: 8px; background: #f1f5f9; overflow: hidden; }
    .progress-bar-custom { height: 100%; }
</style>

@php
    $sureler = $kuyruk['ogeler'] ?? [];
    $sureGoster = array_slice($sureler, 0, 6);
    $turEtiket = ['lisans' => 'Lisans', 'yazarkasa' => 'Yazar kasa'];
@endphp

<div class="container-fluid py-3 px-md-4 desk-page">

    <div class="d-flex align-items-center justify-content-between gap-2 mb-3">
        <div>
            <div class="desk-kicker">Desk</div>
            <h1 class="h5 fw-bold mb-0">Genel bakış</h1>
        </div>
        <div class="text-muted small">
            {{ $now->locale('tr')->translatedFormat('d MMMM Y') }}
            · <span id="liveClock">{{ $now->format('H:i') }}</span>
        </div>
    </div>

    <div class="desk-kpi d-flex flex-wrap mb-3">
        <div class="desk-kpi-item border-end flex-fill">
            <p class="desk-kicker">Bugün</p>
            <div class="val">₺{{ number_format($systemStats['bugun_ciro'], 0, ',', '.') }}</div>
        </div>
        <div class="desk-kpi-item border-end flex-fill">
            <p class="desk-kicker">Bu ay</p>
            <div class="val">₺{{ number_format($systemStats['buay_ciro'], 0, ',', '.') }}</div>
        </div>
        <div class="desk-kpi-item border-end flex-fill">
            <p class="desk-kicker">Müşteri</p>
            <div class="val">{{ $totalCustomers }}</div>
        </div>
        <div class="desk-kpi-item border-end flex-fill">
            <p class="desk-kicker">Aktif paket</p>
            <div class="val">{{ $networkStats['aktif_lisans'] }}</div>
        </div>
        <div class="desk-kpi-item flex-fill">
            <p class="desk-kicker">Bayi</p>
            <div class="val">{{ $totalBayi }}</div>
        </div>
    </div>
    <p class="small text-muted mb-3" style="font-size:.75rem">Aktif paket = paket satırı, sipariş kartı değil. Ciro liste fiyatıdır.</p>

    <div class="row g-3 mb-3">
        <div class="col-lg-7">
            <div class="desk-box">
                <div class="d-flex align-items-center justify-content-between gap-2 mb-1">
                    <div>
                        <h2 class="h6 fw-bold mb-0">Yaklaşan süreler</h2>
                        <p class="text-muted mb-0" style="font-size:.75rem">90 gün · lisans ve yazar kasa</p>
                    </div>
                    <div class="d-flex gap-2 small">
                        <a href="{{ route('UpcomingLicenses', ['gun' => 90]) }}" class="text-decoration-none">Lisans {{ $kuyruk['lisans_sayisi'] }}</a>
                        <span class="text-muted">·</span>
                        <a href="{{ route('admin.yazarkasa_report') }}" class="text-decoration-none">Yazar kasa {{ $kuyruk['yazarkasa_sayisi'] }}</a>
                    </div>
                </div>

                @if(count($sureler) === 0)
                    <p class="text-muted mb-0 mt-2" style="font-size:.85rem">90 gün içinde yaklaşan lisans veya yazar kasa yok.</p>
                @else
                    @foreach($sureGoster as $oge)
                        <a class="desk-row" href="{{ $oge['url'] }}">
                            <span class="desk-type desk-type-{{ $oge['tur'] }}">{{ $turEtiket[$oge['tur']] ?? $oge['tur'] }}</span>
                            <span class="flex-grow-1 text-truncate">{{ $oge['musteri'] }} · {{ $oge['baslik'] }}</span>
                            @if(($oge['kalanGun'] ?? 0) < 0)
                                <span class="badge bg-danger">{{ abs($oge['kalanGun']) }} gün geçti</span>
                            @elseif(($oge['kalanGun'] ?? 99) <= 7)
                                <span class="badge bg-danger">{{ $oge['kalanGun'] }} gün</span>
                            @else
                                <span class="text-muted">{{ $oge['kalanGun'] }} gün</span>
                            @endif
                        </a>
                    @endforeach
                    @if(count($sureler) > 6)
                        <p class="small text-muted mb-0 mt-1">İlk 6 kayıt. Listelerden devam.</p>
                    @endif
                @endif
            </div>
        </div>
        <div class="col-lg-5">
            <div class="desk-box desk-finance h-100">
                <p class="desk-kicker mb-1">Kümülatif ciro</p>
                <div class="val mb-1">₺{{ number_format($systemStats['toplam_ciro'], 2, ',', '.') }}</div>
                <p class="text-muted mb-2" style="font-size:.75rem">{{ $systemStats['toplam_adet'] }} satış · {{ $systemStats['bugun_adet'] }} bugün, {{ $systemStats['buay_adet'] }} bu ay</p>
                <div id="revenueChart" style="height: 150px;"></div>
            </div>
        </div>
    </div>

    <div class="desk-box">
        <div class="d-flex justify-content-between mb-2">
            <h2 class="h6 fw-bold mb-0">Paket dağılımı</h2>
            <span class="text-muted" style="font-size:.75rem">Aktif paket satırı</span>
        </div>
        <div class="row g-2">
            @php
                $colors = ['#f89d1d', '#334155', '#2563eb', '#059669', '#d97706'];
                $colorIndex = 0;
                $totalActive = $networkStats['aktif_lisans'] > 0 ? $networkStats['aktif_lisans'] : 1;
                $topDagilim = collect($networkStats['dagilim'])->sortDesc()->take(8);
            @endphp
            @forelse($topDagilim as $paketName => $count)
                @php
                    $percent = ($count / $totalActive) * 100;
                    $currentColor = $colors[$colorIndex % count($colors)];
                    $colorIndex++;
                @endphp
                <div class="col-md-6 col-xl-3">
                    <div class="d-flex justify-content-between" style="font-size:.78rem">
                        <span class="text-truncate pe-2">{{ $paketName }}</span>
                        <span>{{ $count }}</span>
                    </div>
                    <div class="progress-thin mt-1">
                        <div class="progress-bar-custom" style="width: {{ $percent }}%; background: {{ $currentColor }};"></div>
                    </div>
                </div>
            @empty
                <p class="text-muted small mb-0">Aktif paket yok.</p>
            @endforelse
        </div>
    </div>
</div>

<script>
    document.addEventListener("DOMContentLoaded", () => {
        setInterval(() => {
            const el = document.getElementById('liveClock');
            if (el) {
                el.textContent = new Date().toLocaleTimeString('tr-TR', {
                    hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Istanbul'
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

        var chart = new ApexCharts(document.querySelector("#revenueChart"), {
            series: [{ name: 'Ciro', data: {!! json_encode($chartData['data']) !!} }],
            chart: {
                type: 'area', height: 150,
                fontFamily: 'Plus Jakarta Sans, sans-serif',
                toolbar: { show: false }, sparkline: { enabled: false }
            },
            colors: ['#f89d1d'],
            stroke: { curve: 'smooth', width: 2 },
            fill: { type: 'solid', opacity: 0.12 },
            dataLabels: { enabled: false },
            xaxis: {
                categories: {!! json_encode($chartData['labels']) !!},
                axisBorder: { show: false }, axisTicks: { show: false },
                labels: { style: { colors: '#64748b', fontSize: '10px' } }
            },
            yaxis: {
                labels: { style: { colors: '#64748b', fontSize: '10px' }, formatter: formatTlAxis }
            },
            grid: { borderColor: '#e2e8f0', strokeDashArray: 3, padding: { left: 4, right: 8 } },
            tooltip: {
                y: { formatter: function (val) { return '₺' + new Intl.NumberFormat('tr-TR').format(val); } }
            }
        });
        chart.render();
    });
</script>

@endsection
