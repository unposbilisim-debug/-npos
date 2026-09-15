<?php

namespace App\Providers;

use App\Services\BugunKuyrugu;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\View;
use Illuminate\Support\ServiceProvider;

class AppServiceProvider extends ServiceProvider
{
    public function register(): void
    {
        //
    }

    public function boot(): void
    {
        View::composer('layouts.app', function ($view) {
            $user = Auth::user();
            $empty = [
                'toplam' => 0,
                'lisans_sayisi' => 0,
                'yazarkasa_sayisi' => 0,
                'onizleme' => [],
            ];

            if (!$user || ($user->role ?? null) !== 'admin') {
                $view->with('bugunKuyruguBell', $empty);
                return;
            }

            try {
                $view->with('bugunKuyruguBell', app(BugunKuyrugu::class)->ozet());
            } catch (\Throwable $e) {
                $view->with('bugunKuyruguBell', $empty);
            }
        });
    }
}
