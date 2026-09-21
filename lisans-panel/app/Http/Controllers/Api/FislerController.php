<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\LicenseFis;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

/**
 * ÜnPOS uygulamasının çağırdığı endpoint'leri karşılar:
 *   POST   /api/license/fisler/list
 *   POST   /api/license/fisler/add
 *   PUT    /api/license/fisler/update
 *   DELETE /api/license/fisler/delete
 */
class FislerController extends Controller
{
    /** Tek yanıtta belleği patlatmamak için üst sınır. */
    private const LIST_LIMIT_MAX = 250;

    public function list(Request $request)
    {
        $licenseKey = $request->attributes->get('license_key');

        $limit = (int) $request->input('limit', 150);
        if ($limit <= 0) {
            $limit = 150;
        }
        $limit = min($limit, self::LIST_LIMIT_MAX);

        $since = $request->input('since')
            ?? $request->input('updated_since')
            ?? $request->input('tarih_min');
        $until = $request->input('until') ?? $request->input('tarih_max');
        $afterId = (int) $request->input('after_id', 0);

        // Eloquent + date/json cast binlerce satırda 256MB'ı aşıyor (PWA köprüsü düşüyor).
        $q = DB::table('license_fisler')->where('license_key', $licenseKey);

        if (is_string($since) && $since !== '') {
            $q->where('tarih', '>=', $since);
        } else {
            $q->where('tarih', '>=', now()->subDays(14)->toDateString());
        }
        if (is_string($until) && $until !== '') {
            $q->where('tarih', '<=', $until);
        }
        if ($afterId > 0) {
            $q->where('id', '>', $afterId);
        }

        $rows = $q->orderByDesc('tarih')
            ->orderByDesc('id')
            ->limit($limit)
            ->get();

        $fisler = [];
        foreach ($rows as $row) {
            $fisler[] = $this->hafifFis((array) $row);
        }

        return response()->json([
            'success' => true,
            'fisler' => $fisler,
            'limit' => $limit,
        ]);
    }

    public function add(Request $request)
    {
        $licenseKey = $request->attributes->get('license_key');
        $payload    = $request->input('fis', []);

        try {
            $fields = $this->extractFields($payload);

            // pos_id varsa veya fis_no varsa eşleştirme yap
            $product = null;

            // 1) pos_id ile ara (varsa en güvenilir)
            if (!empty($fields['pos_id'])) {
                $product = LicenseFis::where('license_key', $licenseKey)
                    ->where('pos_id', $fields['pos_id'])
                    ->first();
            }

            // 2) fis_no ile ara
            if (!$product && !empty($fields['fis_no'])) {
                $product = LicenseFis::where('license_key', $licenseKey)
                    ->where('fis_no', $fields['fis_no'])
                    ->first();
            }

            $fields['license_key'] = $licenseKey;

            if ($product) {
                $product->fill($fields)->save();
                $fis = $product;
            } else {
                $fis = LicenseFis::create($fields);
            }

            return response()->json([
                'success' => true,
                'fis'     => $fis,
            ]);
        } catch (\Throwable $e) {
            Log::error('[FIS ADD ERROR]', [
                'message' => $e->getMessage(),
                'file'    => $e->getFile(),
                'line'    => $e->getLine(),
                'payload' => $payload,
            ]);

            return response()->json([
                'success' => false,
                'message' => 'Fiş eklenirken hata: ' . $e->getMessage(),
            ], 500);
        }
    }

    public function update(Request $request)
    {
        $licenseKey = $request->attributes->get('license_key');
        $id         = $request->input('id');
        $payload    = $request->input('fis', []);

        if (!$id) {
            return response()->json(['success' => false, 'message' => 'id zorunlu'], 422);
        }

        $fis = LicenseFis::where('license_key', $licenseKey)
            ->where('id', $id)
            ->first();

        if (!$fis) {
            return response()->json(['success' => false, 'message' => 'Fiş bulunamadı'], 404);
        }

        try {
            $fields = $this->extractFields($payload, $fis);
            $fis->fill($fields)->save();

            return response()->json([
                'success' => true,
                'fis'     => $fis,
            ]);
        } catch (\Throwable $e) {
            Log::error('[FIS UPDATE ERROR]', [
                'message' => $e->getMessage(),
                'file'    => $e->getFile(),
                'line'    => $e->getLine(),
                'payload' => $payload,
            ]);

            return response()->json([
                'success' => false,
                'message' => 'Fiş güncellenirken hata: ' . $e->getMessage(),
            ], 500);
        }
    }

    public function delete(Request $request)
    {
        $licenseKey = $request->attributes->get('license_key');
        $id         = $request->input('id');

        if (!$id) {
            return response()->json(['success' => false, 'message' => 'id zorunlu'], 422);
        }

        LicenseFis::where('license_key', $licenseKey)
            ->where('id', $id)
            ->delete();

        return response()->json(['success' => true]);
    }

    /**
     * Payload'dan tüm alanları çıkar — POS'tan gelen TÜM verileri kabul eder
     */
    private function extractFields(array $payload, ?LicenseFis $fallback = null): array
    {
        $get = function ($payload, $keys, $default = null) use ($fallback) {
            foreach ((array) $keys as $k) {
                if (array_key_exists($k, $payload) && $payload[$k] !== null && $payload[$k] !== '') {
                    return $payload[$k];
                }
            }
            // Fallback'tan al (update için)
            if ($fallback) {
                foreach ((array) $keys as $k) {
                    if (isset($fallback->{$k}) && $fallback->{$k} !== null && $fallback->{$k} !== '') {
                        return $fallback->{$k};
                    }
                }
            }
            return $default;
        };

		// POS'tan gelen ödeme tipini olduğu gibi kabul et — tüm ödeme tipleri serbest
        $odemeTipi = $get($payload, ['odeme_tipi'], 'nakit');

        return [
            'fis_no'      => $get($payload, ['fis_no'], ''),
            'adisyon_no'  => $get($payload, ['adisyon_no']),
            'tarih'       => $get($payload, ['tarih'], now()->toDateString()),
            'toplam'      => (float) $get($payload, ['toplam'], 0),
            'ara_toplam'  => (float) $get($payload, ['ara_toplam'], 0),
            'iskonto'     => (float) $get($payload, ['iskonto'], 0),
            'odeme_tipi'  => $odemeTipi,
            'sale_type'   => $get($payload, ['sale_type']),
            'kasiyer'     => $get($payload, ['kasiyer']),
            'kisi_sayisi' => (int) $get($payload, ['kisi_sayisi'], 1),
            'masa_adi'    => $get($payload, ['masa_adi']),
            'urunler'     => $get($payload, ['urunler'], []),
            'odemeler'    => $get($payload, ['odemeler'], []),
            'pos_id'      => $get($payload, ['pos_id']),
		    'provider_info' => $get($payload, ['provider_info']),
        ];
    }

    /** Liste yanıtını küçük tut: JSON kolonlarını çöz, gömülü görselleri at. */
    private function hafifFis(array $row): array
    {
        foreach (['urunler', 'odemeler', 'provider_info'] as $alan) {
            if (!array_key_exists($alan, $row)) {
                continue;
            }
            $v = $row[$alan];
            if (is_string($v) && $v !== '') {
                $d = json_decode($v, true);
                $row[$alan] = is_array($d) ? $d : $v;
            }
        }
        if (isset($row['urunler']) && is_array($row['urunler'])) {
            $row['urunler'] = array_map(function ($u) {
                if (!is_array($u)) {
                    return $u;
                }
                unset($u['image'], $u['resim'], $u['image_url'], $u['imageUrl'], $u['foto'], $u['photo']);
                return $u;
            }, $row['urunler']);
        }
        return $row;
    }
}
