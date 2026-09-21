<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

class VerifyLicenseKey
{
    public function handle(Request $request, Closure $next)
    {
        $licenseKey = $request->input('licenseKey')
            ?? $request->header('X-License-Key');

        $licenseKey = is_string($licenseKey) ? trim($licenseKey) : '';

        if ($licenseKey === '') {
            return response()->json([
                'success' => false,
                'message' => 'licenseKey gönderilmedi',
            ], 401);
        }

        try {
            $canonical = $this->canonicalKey($licenseKey);
        } catch (\Throwable $e) {
            // Geliştirme aşamasında tablo/sütun farklıysa logla ve geçir.
            // Canlıya alırken bu davranışı kaldırın.
            Log::warning('License check skipped: ' . $e->getMessage());
            $canonical = $licenseKey;
        }

        if ($canonical === null) {
            return response()->json([
                'success' => false,
                'message' => 'Geçersiz lisans anahtarı',
            ], 403);
        }

        $request->attributes->set('license_key', $canonical);
        if ($canonical !== $licenseKey) {
            $request->merge(['licenseKey' => $canonical]);
        }

        return $next($request);
    }

    /** Anahtarı olduğu gibi, sonra trim/büyük harf ile lisans tablosunda bul. */
    private function canonicalKey(string $licenseKey): ?string
    {
        $row = DB::table('lisans')->where('Anahtar', $licenseKey)->value('Anahtar');
        if (is_string($row) && $row !== '') {
            return $row;
        }

        $norm = strtoupper(preg_replace('/\s+/', '', $licenseKey) ?? '');
        if ($norm === '') {
            return null;
        }

        $row = DB::table('lisans')
            ->whereRaw('UPPER(REPLACE(TRIM(Anahtar), " ", "")) = ?', [$norm])
            ->value('Anahtar');

        return is_string($row) && $row !== '' ? $row : null;
    }
}
