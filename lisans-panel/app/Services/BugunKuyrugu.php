<?php

namespace App\Services;

use App\Models\LisansModel;
use App\Models\Teklif;
use Carbon\Carbon;
use Illuminate\Support\Facades\Cache;

class BugunKuyrugu
{
    public const WINDOW_DAYS = 90;

    private const YAZARKASA_PAKETLER = ['pavo', 'inpos', 'hugin', 'beko'];

    /**
     * Admin Desk + bildirim zili için 90 günlük iş kuyruğu.
     */
    public function forAdmin(bool $useCache = true): array
    {
        if (!$useCache) {
            return $this->build();
        }

        return Cache::remember('bugun_kuyrugu_admin', 60, fn () => $this->build());
    }

    public function ozet(): array
    {
        $full = $this->forAdmin();

        return [
            'toplam' => $full['toplam'],
            'lisans_sayisi' => $full['lisans_sayisi'],
            'yazarkasa_sayisi' => $full['yazarkasa_sayisi'],
            'teklif_sayisi' => $full['teklif_sayisi'],
            'onizleme' => array_slice($full['ogeler'], 0, 8),
        ];
    }

    public function forget(): void
    {
        Cache::forget('bugun_kuyrugu_admin');
    }

    private function build(): array
    {
        $bugun = Carbon::now('Europe/Istanbul')->startOfDay();
        $lisanslar = [];
        $yazarkasa = [];

        $kayitlar = LisansModel::with(['musterix', 'bayilers'])->get();

        foreach ($kayitlar as $lisans) {
            $json = json_decode($lisans->Lisans, true);
            if (!is_array($json)) {
                continue;
            }

            $musteri = $lisans->musterix?->Unvan ?? 'Silinmiş müşteri';
            $bayi = $lisans->bayilers?->name ?? 'Merkez';

            foreach ($this->flattenPaketler($json) as $paket) {
                $pName = $paket['paketName'] ?? null;
                if (!$pName || empty($paket['date'])) {
                    continue;
                }

                $kalanGun = $this->kalanGun($paket['date'], $bugun);
                if ($kalanGun === null || $kalanGun > self::WINDOW_DAYS || $kalanGun < -self::WINDOW_DAYS) {
                    continue;
                }

                $satir = [
                    'tur' => 'lisans',
                    'musteri' => $musteri,
                    'bayi' => $bayi,
                    'baslik' => $pName,
                    'bitisTarihi' => $paket['date'],
                    'kalanGun' => $kalanGun,
                    'url' => route('UpcomingLicenses', ['gun' => self::WINDOW_DAYS]),
                ];

                if ($paket['yazarkasa'] ?? false) {
                    $satir['tur'] = 'yazarkasa';
                    $satir['url'] = route('admin.yazarkasa_report');
                    if (!empty($paket['serial_numbers']) && is_array($paket['serial_numbers'])) {
                        $satir['baslik'] = $pName.' · '.$paket['serial_numbers'][0];
                    }
                    $yazarkasa[] = $satir;
                } else {
                    $lisanslar[] = $satir;
                }
            }
        }

        $teklifler = [];
        foreach (Teklif::query()->where('durum', 'beklemede')->orderByDesc('id')->get() as $teklif) {
            $teklifler[] = [
                'tur' => 'teklif',
                'musteri' => $teklif->musteri ?: 'Müşteri yok',
                'bayi' => '',
                'baslik' => $teklif->baslik ?: ('Teklif '.$teklif->teklif_no),
                'bitisTarihi' => optional($teklif->tarih)->format('d.m.Y'),
                'kalanGun' => null,
                'tutar' => (float) $teklif->genel_toplam,
                'url' => route('OfferList'),
            ];
        }

        usort($lisanslar, fn ($a, $b) => $a['kalanGun'] <=> $b['kalanGun']);
        usort($yazarkasa, fn ($a, $b) => $a['kalanGun'] <=> $b['kalanGun']);

        $ogeler = array_merge($lisanslar, $yazarkasa, $teklifler);

        return [
            'lisanslar' => $lisanslar,
            'yazarkasa' => $yazarkasa,
            'teklifler' => $teklifler,
            'ogeler' => $ogeler,
            'lisans_sayisi' => count($lisanslar),
            'yazarkasa_sayisi' => count($yazarkasa),
            'teklif_sayisi' => count($teklifler),
            'toplam' => count($ogeler),
            'pencere' => self::WINDOW_DAYS,
        ];
    }

    /**
     * @return list<array{paketName:string,date:?string,serial_numbers:array,yazarkasa:bool}>
     */
    private function flattenPaketler(array $json): array
    {
        $out = [];

        foreach ($json as $key => $item) {
            if (!is_array($item)) {
                continue;
            }

            $ykGrup = null;
            if (is_string($key) && strtolower($key) === 'yazarkasa') {
                $ykGrup = $item;
            }
            foreach ($item as $k => $v) {
                if (is_string($k) && strtolower($k) === 'yazarkasa' && is_array($v)) {
                    $ykGrup = $v;
                    break;
                }
            }

            if (is_array($ykGrup)) {
                foreach ($ykGrup as $ykItem) {
                    if (is_array($ykItem) && isset($ykItem['paketName'])) {
                        $out[] = $this->normalizePaket($ykItem, true);
                    }
                }
                continue;
            }

            if (isset($item['paketName'])) {
                $out[] = $this->normalizePaket($item, $this->isYazarkasaPaket($item));
                continue;
            }

            foreach ($item as $sub) {
                if (is_array($sub) && isset($sub['paketName'])) {
                    $out[] = $this->normalizePaket($sub, $this->isYazarkasaPaket($sub));
                }
            }
        }

        return $out;
    }

    private function normalizePaket(array $item, bool $yazarkasa): array
    {
        $serials = $item['serial_numbers'] ?? [];
        if (!is_array($serials)) {
            $serials = [];
        }

        if (!$yazarkasa && $this->isYazarkasaPaket($item)) {
            $yazarkasa = true;
        }

        return [
            'paketName' => $item['paketName'],
            'date' => $item['date'] ?? null,
            'serial_numbers' => $serials,
            'yazarkasa' => $yazarkasa,
        ];
    }

    private function isYazarkasaPaket(array $item): bool
    {
        $name = strtolower((string) ($item['paketName'] ?? ''));
        if (in_array($name, self::YAZARKASA_PAKETLER, true)) {
            return true;
        }

        return !empty($item['serial_numbers']) && is_array($item['serial_numbers']);
    }

    private function kalanGun(string $tarih, Carbon $bugun): ?int
    {
        $tarih = trim($tarih);

        try {
            if (preg_match('/^\d{2}\.\d{2}\.\d{4}$/', $tarih)) {
                $bitis = Carbon::createFromFormat('d.m.Y', $tarih, 'Europe/Istanbul')->startOfDay();
            } elseif (preg_match('/^\d{4}-\d{2}-\d{2}$/', $tarih)) {
                $bitis = Carbon::createFromFormat('Y-m-d', $tarih, 'Europe/Istanbul')->startOfDay();
            } else {
                return null;
            }

            return (int) $bugun->diffInDays($bitis, false);
        } catch (\Exception $e) {
            return null;
        }
    }
}
