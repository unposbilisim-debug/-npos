<?php

namespace App\Http\Controllers;

use App\Models\BayiModel;
use App\Models\LisansModel;
use App\Models\LisansPaketModel;
use App\Models\MusteriModel;
use App\Services\BugunKuyrugu;
use Illuminate\Support\Facades\Auth;
use Carbon\Carbon;

class AdminController extends Controller
{
    public function dashboard()
    {
        $user = Auth::user();
        $now = Carbon::now('Europe/Istanbul');

        $paketFiyatlari = LisansPaketModel::pluck('PaketFiyati', 'PaketName')->toArray();

        $totalCustomers = MusteriModel::count();
        $totalBayi = BayiModel::count();

        $tumLisanslar = LisansModel::with(['musterix', 'bayilers'])
            ->orderBy('created_at', 'desc')
            ->get();

        $systemStats = [
            'bugun_ciro' => 0, 'bugun_adet' => 0,
            'buay_ciro' => 0, 'buay_adet' => 0,
            'toplam_ciro' => 0, 'toplam_adet' => 0,
        ];

        $networkStats = [
            'aktif_lisans' => 0,
            'dagilim' => [],
        ];

        $sonSatislar = [];

        $chartDataRaw = [];
        for ($i = 5; $i >= 0; $i--) {
            $dt = $now->copy()->subMonths($i);
            $key = $dt->format('Y-m');
            $chartDataRaw[$key] = [
                'label' => $dt->locale('tr')->translatedFormat('F'),
                'total' => 0,
            ];
        }

        $startToday = $now->copy()->startOfDay();
        $startMonth = $now->copy()->startOfMonth();

        foreach ($tumLisanslar as $lisans) {
            $createdAt = Carbon::parse($lisans->created_at)->timezone('Europe/Istanbul');
            $isSatis = (strtolower(trim($lisans->Tipi ?? '')) === 'satis');

            $lisansData = json_decode($lisans->Lisans, true);
            if (!$lisansData) {
                continue;
            }

            $lisansTutari = 0;
            $itemsToCheck = [];

            foreach ($lisansData as $item) {
                if (isset($item['yazarkasa']) && is_array($item['yazarkasa'])) {
                    foreach ($item['yazarkasa'] as $yk) {
                        if (isset($yk['paketName'])) {
                            $itemsToCheck[] = $yk;
                        }
                    }
                } elseif (isset($item['paketName'])) {
                    $itemsToCheck[] = $item;
                } elseif (is_array($item)) {
                    foreach ($item as $sub) {
                        if (is_array($sub) && isset($sub['paketName'])) {
                            $itemsToCheck[] = $sub;
                        }
                    }
                }
            }

            foreach ($itemsToCheck as $paket) {
                $pName = $paket['paketName'];
                $aktifMi = (isset($paket['status']) && $paket['status'] == 1);

                if ($aktifMi) {
                    $fiyat = isset($paketFiyatlari[$pName]) ? (float) $paketFiyatlari[$pName] : 0;
                    $lisansTutari += $fiyat;

                    $networkStats['aktif_lisans']++;
                    if (!isset($networkStats['dagilim'][$pName])) {
                        $networkStats['dagilim'][$pName] = 0;
                    }
                    $networkStats['dagilim'][$pName]++;
                }
            }

            if (count($sonSatislar) < 10) {
                $sonSatislar[] = $lisans;
            }

            if ($isSatis) {
                $systemStats['toplam_ciro'] += $lisansTutari;
                $systemStats['toplam_adet']++;

                if ($createdAt >= $startToday) {
                    $systemStats['bugun_ciro'] += $lisansTutari;
                    $systemStats['bugun_adet']++;
                }

                if ($createdAt >= $startMonth) {
                    $systemStats['buay_ciro'] += $lisansTutari;
                    $systemStats['buay_adet']++;
                }

                $monthKey = $createdAt->format('Y-m');
                if (isset($chartDataRaw[$monthKey])) {
                    $chartDataRaw[$monthKey]['total'] += $lisansTutari;
                }
            }
        }

        $chartData = ['labels' => [], 'data' => []];
        foreach ($chartDataRaw as $item) {
            $chartData['labels'][] = $item['label'];
            $chartData['data'][] = $item['total'];
        }

        $kuyruk = app(BugunKuyrugu::class)->forAdmin();

        return view('admin.dashboard', compact(
            'user',
            'now',
            'totalBayi',
            'totalCustomers',
            'systemStats',
            'networkStats',
            'kuyruk',
            'sonSatislar',
            'paketFiyatlari',
            'chartData'
        ));
    }
}
