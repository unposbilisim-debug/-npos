<?php

namespace App\Http\Controllers;

use App\Models\LisansPaketModel;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;

class ProgramPaketleriController extends Controller
{
    public function ProgramPackages()
    {
        $lisanspaket = LisansPaketModel::orderBy('PaketAdi')->get();
        $etiket = [
            'paket' => 'Ana paketler',
            'modul' => 'Modüller',
            'uygulama' => 'Uygulamalar',
            'yazarkasa' => 'Yazar kasa',
        ];
        $gruplar = [];
        foreach ($etiket as $tip => $baslik) {
            $gruplar[$tip] = [
                'baslik' => $baslik,
                'satirlar' => $lisanspaket->where('PaketTipi', $tip)->values(),
            ];
        }
        foreach ($lisanspaket->groupBy(fn ($p) => $p->PaketTipi ?: 'diger') as $tip => $satirlar) {
            if (! isset($gruplar[$tip])) {
                $gruplar[$tip] = [
                    'baslik' => $tip === 'diger' ? 'Diğer' : ucfirst((string) $tip),
                    'satirlar' => $satirlar->values(),
                ];
            }
        }

        return view('lisanspaket.index', compact('lisanspaket', 'gruplar'));
    }

    public function AddProgramPackages(Request $request)
    {
        try {
            $request->validate([
                'PaketAdi' => 'required',
                'PaketAciklama' => 'required',
            ]);

            $lisanspaket = new LisansPaketModel();
            $lisanspaket->PaketAdi = $request->input('PaketAdi');
            $lisanspaket->PaketFiyati = $request->input('PaketFiyati');
            $lisanspaket->PaketAciklama = $request->input('PaketAciklama');
            $lisanspaket->PaketSira = 0;
            $lisanspaket->PaketName = $request->input('PaketName');
            $lisanspaket->PaketTipi = $request->input('PaketTipi');
            $lisanspaket->PaketDurum = 1;
            $lisanspaket->AnaAlis = $request->input('AnaAlis');
            $lisanspaket->AltBayiAlis = 0;
            $lisanspaket->save();
            $lisanspaketID = $lisanspaket->id;

            activity('Paket İşlemleri')
            ->causedBy(null) 
            ->tap(function ($activity) {
                $activity->causer_type = 'Add'; 
                $activity->causer_id =  Auth::user()->id; 
            })
            ->withProperties(['id' => $lisanspaketID])
            ->log('Paket Oluşturuldu');

            return redirect()->route('ProgramPackages')->with('success', 'Program Paketi Başarılı Şekilde Oluşturuldu')->with('run_success_js', true);
        } catch (\Illuminate\Validation\ValidationException $e) {
            return redirect()->route('ProgramPackages')->with('warning', 'Formda eksik veya hatalı veriler var')->with('run_warning_js', true);
        }
    }
    
    public function UpdateProgramPackages(Request $request,$id)
    {
        try {
            $id = decrypt(urldecode($id));
            $lisanspaket = LisansPaketModel::find($id);
            $lisanspaket->PaketAdi = $request->input('PaketAdi');
            $lisanspaket->PaketFiyati = $request->input('PaketFiyati');
            $lisanspaket->PaketAciklama = $request->input('PaketAciklama');
            $lisanspaket->PaketName = $request->input('PaketName');
            $lisanspaket->PaketTipi = $request->input('PaketTipi');
            $lisanspaket->AnaAlis = $request->input('AnaAlis');
            $lisanspaket->save();
            $lisanspaketID = $lisanspaket->id;
    
            activity('Paket İşlemleri')
            ->causedBy(null) 
            ->tap(function ($activity) {
                $activity->causer_type = 'Add'; 
                $activity->causer_id = Auth::user()->id; 
            })
            ->withProperties(['id' => $lisanspaketID])
            ->log('Paket Güncellendi');
    
            return redirect()->route('ProgramPackages')->with('success', 'Program Paketi Başarılı Şekilde Güncellendi')->with('run_success_js', true);
        } catch (\Illuminate\Validation\ValidationException $e) {
            return redirect()->route('ProgramPackages')->with('warning', 'Formda eksik veya hatalı veriler var')->with('run_warning_js', true);
        }
    }

    public function DeleteProgramPackages(Request $request, $id)
    {
        try {
            $id = decrypt(urldecode($id));
            $lisanspaket = LisansPaketModel::find($id);
            if (! $lisanspaket) {
                return redirect()->route('ProgramPackages')->with('error', 'Paket bulunamadı')->with('run_error_js', true);
            }

            $paketId = $lisanspaket->id;
            $lisanspaket->delete();

            activity('Paket İşlemleri')
                ->causedBy(null)
                ->tap(function ($activity) {
                    $activity->causer_type = 'Delete';
                    $activity->causer_id = Auth::user()->id;
                })
                ->withProperties(['id' => $paketId])
                ->log('Paket Silindi');

            return redirect()->route('ProgramPackages')->with('success', 'Paket silindi')->with('run_success_js', true);
        } catch (\Throwable $e) {
            return redirect()->route('ProgramPackages')->with('error', 'Paket silinemedi')->with('run_error_js', true);
        }
    }
}
