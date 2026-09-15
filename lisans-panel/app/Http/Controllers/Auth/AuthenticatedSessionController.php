<?php

namespace App\Http\Controllers\Auth;

use App\Http\Controllers\Controller;
use App\Http\Requests\Auth\LoginRequest;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\View\View;

class AuthenticatedSessionController extends Controller
{
    /**
     * Display the login view.
     */
    public function create(): View
    {
        return view('auth.login');
    }

    /**
     * Handle an incoming authentication request.
     */
    public function store(LoginRequest $request): RedirectResponse
    {
        $request->authenticate();

        $request->session()->regenerate();

        $url = '/Desk';
        if ($request->user()->role == 'admin') {
            $url = '/Desk';
        } elseif ($request->user()->role == 'agent') {
            $url = '/agent/dashboard';
        }

        $intended = $request->session()->pull('url.intended');
        $intendedPath = parse_url((string) $intended, PHP_URL_PATH) ?? '';
        $intendedPath = rtrim($intendedPath, '/') ?: '/';

        if ($intended && !in_array($intendedPath, ['/dashboard', '/'], true)) {
            return redirect()->to($intended);
        }

        return redirect()->to($url);
    }

    /**
     * Destroy an authenticated session.
     */
    public function destroy(Request $request): RedirectResponse
    {
        Auth::guard('web')->logout();

        $request->session()->invalidate();

        $request->session()->regenerateToken();

        return redirect('/');
    }
}
