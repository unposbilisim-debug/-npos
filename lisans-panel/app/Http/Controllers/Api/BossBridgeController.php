<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\LisansModel;
use App\Models\MusteriModel;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Str;

/**
 * Restoran ÜnPOS Boss köprüsü — muhasebe patron-bridge'den AYRI kuyruk.
 * UI: boss.unposbarkod.com (/canli) veya /boss-mobil
 */
class BossBridgeController extends Controller
{
    private function key(Request $request): string
    {
        return (string) ($request->attributes->get('license_key')
            ?? $request->input('licenseKey')
            ?? $request->header('X-License-Key')
            ?? '');
    }

    private function onlineKey(string $licenseKey): string
    {
        return 'boss_bridge_online_' . hash('sha256', $licenseKey);
    }

    private function jobsKey(string $licenseKey): string
    {
        return 'boss_bridge_jobs_' . hash('sha256', $licenseKey);
    }

    private function resultKey(string $id): string
    {
        return 'boss_bridge_result_' . $id;
    }

    /** Köprü yalnızca patron ve kurye uçlarını geçirir. */
    private function yolIzinliMi(string $path): bool
    {
        foreach (['/patron', '/kurye'] as $onEk) {
            if ($path === $onEk
                || str_starts_with($path, $onEk . '/')
                || str_starts_with($path, $onEk . '?')) {
                return true;
            }
        }
        return false;
    }

    private function uzunIsteginSuresiniAc(): void
    {
        @set_time_limit(120);
        @ignore_user_abort(false);
    }

    private function userKey(string $kullaniciAdi): string
    {
        return 'boss_bridge_user_' . hash('sha256', strtolower(trim($kullaniciAdi)));
    }

    private function storeKey(string $kod): string
    {
        $n = strtoupper(preg_replace('/[^A-Z0-9]/', '', $kod) ?? '');
        return 'boss_bridge_store_' . hash('sha256', $n);
    }

    private function licenseStoreKey(string $licenseKey): string
    {
        return 'boss_bridge_lk_store_' . hash('sha256', $licenseKey);
    }

    private function normalizeStoreCode(string $kod): string
    {
        return strtoupper(preg_replace('/[^A-Z0-9]/', '', $kod) ?? '');
    }

    private function formatStoreCode(string $kod): string
    {
        $n = $this->normalizeStoreCode($kod);
        return strlen($n) === 6 ? substr($n, 0, 3) . '-' . substr($n, 3) : $n;
    }

    private function registerStore(string $licenseKey, Request $request): void
    {
        $kod = $this->normalizeStoreCode((string) $request->input('magazaKodu', ''));
        if (strlen($kod) < 6) {
            return;
        }
        Cache::put($this->storeKey($kod), [
            'licenseKey' => $licenseKey,
            'at' => now()->toIso8601String(),
        ], now()->addHours(24));
        Cache::put($this->licenseStoreKey($licenseKey), [
            'magazaKodu' => $this->formatStoreCode($kod),
            'at' => now()->toIso8601String(),
        ], now()->addHours(24));
    }

    private function registerUsers(string $licenseKey, $names, string $rol): void
    {
        if (!is_array($names)) {
            return;
        }
        foreach ($names as $name) {
            $n = strtolower(trim((string) $name));
            if ($n === '' || strlen($n) < 2) {
                continue;
            }
            Cache::put($this->userKey($n), [
                'licenseKey' => $licenseKey,
                'rol' => $rol,
                'at' => now()->toIso8601String(),
            ], now()->addMinutes(45));
        }
    }

    /** Köprü her hello/poll'da patron ve kurye giriş adlarını bildirir. */
    private function registerBridgeUsers(string $licenseKey, Request $request): void
    {
        $this->registerUsers($licenseKey, $request->input('patrons', []), 'patron');
        $this->registerUsers($licenseKey, $request->input('couriers', []), 'kurye');
    }

    public function hello(Request $request)
    {
        $lk = $this->key($request);
        Cache::put($this->onlineKey($lk), [
            'at' => now()->toIso8601String(),
            'v' => (int) $request->input('v', 1),
        ], now()->addMinutes(45));
        $this->registerBridgeUsers($lk, $request);
        $this->registerStore($lk, $request);

        return response()->json([
            'success' => true,
            'message' => 'Boss köprü online',
            'pollPath' => '/api/license/boss-bridge/poll',
        ]);
    }

    public function resolve(Request $request)
    {
        $kod = $this->normalizeStoreCode((string) (
            $request->input('magazaKodu')
            ?? $request->input('storeCode')
            ?? ''
        ));
        if ($kod !== '') {
            if (strlen($kod) < 6) {
                return response()->json([
                    'success' => false,
                    'hata' => 'Mağaza kodu 6 karakter olmalı',
                ], 422);
            }
            $map = Cache::get($this->storeKey($kod));
            if (!$map || empty($map['licenseKey'])) {
                return response()->json([
                    'success' => false,
                    'hata' => 'Mağaza kodu bulunamadı veya mağaza çevrimdışı. ÜnPOS’ta Patron Köprü açık mı?',
                    'online' => false,
                ], 404);
            }
            $lk = $map['licenseKey'];
            $online = Cache::get($this->onlineKey($lk));
            $liste = $this->subeListesi($lk);
            return response()->json([
                'success' => true,
                'online' => !!$online,
                'licenseKey' => $lk,
                'magazaKodu' => $this->formatStoreCode($kod),
                'lastSeen' => $online['at'] ?? ($map['at'] ?? null),
                'firma' => $liste['firma'],
                'subeler' => $liste['subeler'],
            ]);
        }

        $ka = strtolower(trim((string) ($request->input('kullaniciAdi')
            ?? $request->input('username')
            ?? '')));
        if ($ka === '' || strlen($ka) < 2) {
            return response()->json([
                'success' => false,
                'hata' => 'Mağaza kodu veya giriş adı gerekli',
            ], 422);
        }

        $map = Cache::get($this->userKey($ka));
        if (!$map || empty($map['licenseKey'])) {
            return response()->json([
                'success' => false,
                'hata' => 'Bu giriş adı bulunamadı veya mağaza çevrimdışı.',
                'online' => false,
            ], 404);
        }

        $lk = $map['licenseKey'];
        $online = Cache::get($this->onlineKey($lk));
        $liste = $this->subeListesi($lk);
        return response()->json([
            'success' => true,
            'online' => !!$online,
            'licenseKey' => $lk,
            'rol' => $map['rol'] ?? 'patron',
            'lastSeen' => $online['at'] ?? ($map['at'] ?? null),
            'firma' => $liste['firma'],
            'subeler' => $liste['subeler'],
        ]);
    }

    private function licenseFromRequest(Request $request): string
    {
        $lk = trim((string) ($request->input('licenseKey')
            ?? $request->header('X-License-Key')
            ?? ''));
        if ($lk !== '') {
            return $lk;
        }
        $kod = $this->normalizeStoreCode((string) (
            $request->input('magazaKodu')
            ?? $request->input('storeCode')
            ?? ''
        ));
        if (strlen($kod) >= 6) {
            $map = Cache::get($this->storeKey($kod));
            return (string) ($map['licenseKey'] ?? '');
        }
        return '';
    }

    private function lisansAktifMi($row): bool
    {
        $d = $row->Durum ?? null;
        if ($d === null || $d === '') {
            return true;
        }
        return (string) $d === '1' || $d === 1 || $d === true;
    }

    /** Aynı müşteriye bağlı lisanslar = mobil şubeler. */
    private function subeListesi(string $licenseKey): array
    {
        $bos = ['firma' => null, 'subeler' => []];
        if ($licenseKey === '') {
            return $bos;
        }

        $mevcut = LisansModel::where('Anahtar', $licenseKey)->first();
        $musteriId = $mevcut?->Musteri;
        $musteri = $musteriId ? MusteriModel::find($musteriId) : null;

        $satirlar = $musteriId
            ? LisansModel::where('Musteri', $musteriId)->get()
            : collect($mevcut ? [$mevcut] : []);

        $subeler = [];
        foreach ($satirlar as $row) {
            if (!$this->lisansAktifMi($row)) {
                continue;
            }
            $anahtar = (string) ($row->Anahtar ?? '');
            if ($anahtar === '') {
                continue;
            }
            $online = Cache::get($this->onlineKey($anahtar));
            $store = Cache::get($this->licenseStoreKey($anahtar));
            $kod = $store['magazaKodu'] ?? null;
            $ad = trim((string) (
                ($row->PcName ?: null)
                ?? ($musteri->TabelaAdi ?? null)
                ?? ($musteri->Unvan ?? null)
                ?? $kod
                ?? ''
            ));
            $subeler[] = [
                'licenseKey' => $anahtar,
                'magazaKodu' => $kod,
                'unvan' => $musteri->Unvan ?? null,
                'tabelaAdi' => $musteri->TabelaAdi ?? null,
                'pcName' => $row->PcName ?: null,
                'ad' => $ad !== '' ? $ad : ($kod ?: 'Şube'),
                'online' => (bool) $online,
                'lastSeen' => $online['at'] ?? ($store['at'] ?? null),
                'secili' => $anahtar === $licenseKey,
            ];
        }

        if (empty($subeler)) {
            $online = Cache::get($this->onlineKey($licenseKey));
            $store = Cache::get($this->licenseStoreKey($licenseKey));
            $subeler[] = [
                'licenseKey' => $licenseKey,
                'magazaKodu' => $store['magazaKodu'] ?? null,
                'unvan' => $musteri->Unvan ?? null,
                'tabelaAdi' => $musteri->TabelaAdi ?? null,
                'pcName' => $mevcut->PcName ?? null,
                'ad' => $musteri->TabelaAdi ?? $musteri->Unvan ?? ($store['magazaKodu'] ?? 'Şube'),
                'online' => (bool) $online,
                'lastSeen' => $online['at'] ?? ($store['at'] ?? null),
                'secili' => true,
            ];
        }

        usort($subeler, function ($a, $b) {
            if ($a['secili'] !== $b['secili']) {
                return $a['secili'] ? -1 : 1;
            }
            if ($a['online'] !== $b['online']) {
                return $a['online'] ? -1 : 1;
            }
            return strcasecmp((string) $a['ad'], (string) $b['ad']);
        });

        return [
            'firma' => $musteri ? [
                'id' => $musteri->id,
                'unvan' => $musteri->Unvan,
                'tabelaAdi' => $musteri->TabelaAdi,
            ] : null,
            'subeler' => $subeler,
        ];
    }

    public function subeler(Request $request)
    {
        $lk = $this->licenseFromRequest($request);
        if ($lk === '') {
            return response()->json([
                'success' => false,
                'hata' => 'Mağaza kodu veya lisans anahtarı gerekli',
            ], 422);
        }

        $liste = $this->subeListesi($lk);
        return response()->json([
            'success' => true,
            'firma' => $liste['firma'],
            'subeler' => $liste['subeler'],
        ]);
    }

    public function poll(Request $request)
    {
        $this->uzunIsteginSuresiniAc();
        $lk = $this->key($request);
        Cache::put($this->onlineKey($lk), [
            'at' => now()->toIso8601String(),
            'v' => (int) $request->input('v', 1),
        ], now()->addMinutes(45));
        $this->registerBridgeUsers($lk, $request);
        $this->registerStore($lk, $request);

        $waitMs = min(20000, max(1000, (int) $request->input('waitMs', 5000)));
        $deadline = microtime(true) + ($waitMs / 1000);
        $jk = $this->jobsKey($lk);

        do {
            $jobs = Cache::get($jk, []);
            if (!empty($jobs)) {
                $job = array_shift($jobs);
                Cache::put($jk, $jobs, now()->addMinutes(10));
                return response()->json([
                    'success' => true,
                    'jobs' => [$job],
                ]);
            }
            usleep(250000);
        } while (microtime(true) < $deadline);

        return response()->json(['success' => true, 'jobs' => []]);
    }

    public function result(Request $request)
    {
        $id = (string) $request->input('id', '');
        if ($id === '') {
            return response()->json(['success' => false, 'message' => 'id zorunlu'], 422);
        }

        Cache::put($this->resultKey($id), [
            'status' => (int) $request->input('status', 200),
            'body' => $request->input('body'),
            'headers' => $request->input('headers', []),
        ], now()->addMinutes(2));

        return response()->json(['success' => true]);
    }

    public function bye(Request $request)
    {
        $lk = $this->key($request);
        Cache::forget($this->onlineKey($lk));
        return response()->json(['success' => true]);
    }

    public function status(Request $request)
    {
        $lk = $this->key($request);
        $online = Cache::get($this->onlineKey($lk));
        return response()->json([
            'success' => true,
            'online' => !!$online,
            'lastSeen' => $online['at'] ?? null,
        ]);
    }

    public function proxy(Request $request)
    {
        $this->uzunIsteginSuresiniAc();
        $lk = $this->key($request);
        $online = Cache::get($this->onlineKey($lk));
        if (!$online) {
            return response()->json([
                'success' => false,
                'hata' => 'Mağaza çevrimdışı. ÜnPOS’ta Patron Köprü açık mı?',
            ], 503);
        }

        $method = strtoupper((string) $request->input('method', 'GET'));
        $path = (string) $request->input('path', '');
        if (!$this->yolIzinliMi($path)) {
            return response()->json([
                'success' => false,
                'message' => 'path /patron veya /kurye ile başlamalı',
            ], 422);
        }

        $id = (string) Str::uuid();
        $job = [
            'id' => $id,
            'method' => $method,
            'path' => $path,
            'headers' => $request->input('headers', []) ?: [],
            'body' => $request->input('body'),
        ];

        $jk = $this->jobsKey($lk);
        $jobs = Cache::get($jk, []);
        $jobs[] = $job;
        Cache::put($jk, $jobs, now()->addMinutes(10));

        $waitMs = min(90000, max(2000, (int) $request->input('waitMs', 8000)));
        $deadline = microtime(true) + ($waitMs / 1000);
        $rk = $this->resultKey($id);

        do {
            $result = Cache::pull($rk);
            if ($result !== null) {
                $status = (int) ($result['status'] ?? 200);
                $body = $result['body'] ?? null;
                return response()->json($body, $status);
            }
            usleep(200000);
        } while (microtime(true) < $deadline);

        return response()->json([
            'success' => false,
            'hata' => 'Mağaza yanıt vermedi (zaman aşımı).',
        ], 504);
    }
}
