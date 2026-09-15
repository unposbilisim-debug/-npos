<?php

namespace App\Http\Controllers;

use App\Models\Ayarlar;
use App\Models\User;
use App\Models\BayiModel;
use App\Models\MusteriModel;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;

class AyarlarController extends Controller
{
    public function Comingsoon()
    {
        return view('Comingsoon');
    }

    public function MicroService()
    {
        return view('MicroService');
    }

    public function PayTR()
    {
        $paytr = Ayarlar::first();
        return view('PayTR', compact('paytr'));
    }

    public function SMS()
    {
        $sms = Ayarlar::first();
        return view('SMS', compact('sms'));
    }

    public function UpdatePayTR(Request $request)
    {

        $ayarlar = Ayarlar::first();
        $ayarlar ->alan6 = $request->input('alan6');
        $ayarlar ->alan7 = $request->input('alan7');
        $ayarlar ->alan8 = $request->input('alan8');

        
        $ayarlar->update();

        return redirect()->back()->with('success', 'PayTR Bilgileri Başarılı Şekilde Güncellendi.')->with('run_success_js', true);
    }
    public function UpdateSMS(Request $request)
    {

        $ayarlar = Ayarlar::first();
        $ayarlar ->alan1 = $request->input('alan1');
        $ayarlar ->alan2 = $request->input('alan2');
        $ayarlar ->alan3 = $request->input('alan3');
        $ayarlar ->alan4 = $request->input('alan4');
        $ayarlar ->alan5 = $request->input('alan5');

        
        $ayarlar->update();

        return redirect()->back()->with('success', 'Sms Bilgileri Başarılı Şekilde Güncellendi.')->with('run_success_js', true);
    } 
 
    public function Certificate()
    {
        $user = Auth::user();

        $bayi = BayiModel::where('UserId', $user->id)->first();

        if ($user->role == 'admin') {
            $Musteriler = MusteriModel::with('kimbubayi')->get();
        } else {
            $kimlikler = [$user->id];

            if ($user->is_main_dealer) {
                $subDealerIds = User::where('parent_id', $user->id)->pluck('id')->toArray();
                $kimlikler = array_merge($kimlikler, $subDealerIds);
            }

            $Musteriler = MusteriModel::with('kimbubayi')->whereIn('Bayi', $kimlikler)->get();
        }

        $bayiAdlari = User::whereIn('id', $Musteriler->pluck('Bayi')->filter()->unique())
            ->pluck('name', 'id');

        return view('sertifika.index', compact('Musteriler', 'bayi', 'bayiAdlari'));
    }
}
