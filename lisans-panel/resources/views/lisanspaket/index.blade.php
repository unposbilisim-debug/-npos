@extends('layouts.app')

@section('content')
    <div class="main-content">
        <!--breadcrumb-->
        <div class="page-breadcrumb d-none d-sm-flex align-items-center mb-3">
            <div class="breadcrumb-title pe-3">ÜnPOS CRM</div>
            <div class="ps-3">
                <nav aria-label="breadcrumb">
                    <ol class="breadcrumb mb-0 p-0">

                        <li class="breadcrumb-item active" aria-current="page">Paket İşlemleri</li>
                    </ol>
                </nav>
            </div>
            <div class="ms-auto">
                <button type="button" class="btn border-0 text-white" style="background: linear-gradient(135deg, #1a237e 0%, #283593 100%);" data-bs-toggle="modal" data-bs-target="#exampleLargeModal">Paket Oluştur</button>
                  
                    <style>
                        .page-breadcrumb {
                           display: flex !important;
                           justify-content: space-between !important;
                           align-items: center !important;
                           flex-wrap: wrap;
                        }
                        
                        .breadcrumb-title, .ps-3 {
                           display: inline-block;
                        }
                        
                        .ms-auto {
                           margin-left: auto !important;
                           margin-top: 0 !important;
                        }
                        
                        @media (max-width: 768px) {
                           .page-breadcrumb {
                              flex-direction: row !important;
                           }
                        }
                     </style>
            </div>
            <!--Model-->
            <div class="modal fade" id="exampleLargeModal" tabindex="-1" aria-hidden="true">
                <div class="modal-dialog modal-lg ">
                    <div class="modal-content border-3" style="border-color: #1a237e !important;">
                        <div class="modal-header ">
                            <h5 class="modal-title"><i class="material-icons-outlined fs-5">domain_add</i> Paket Oluştur</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form method="POST" action="{{ route('AddProgramPackages') }}" enctype="multipart/form-data">
                                @csrf

                                <div class="row mb-3">
                                    <label for="input49" class="col-sm-3 col-form-label">Paket Adı</label>
                                    <div class="col-sm-9">
                                        <div class="input-group">
                                            <span class="input-group-text"><i
                                                    class="material-icons-outlined fs-5">radio_button_checked</i></span>
                                            <input type="text" name="PaketAdi" class="form-control" id="paketadi"
                                                placeholder="ÜnPOS" required>
                                        </div>
                                    </div>
                                </div>
                                <div class="row mb-3">
                                    <label for="input49" class="col-sm-3 col-form-label">Alış / Satış Fiyatı</label>
                                    <div class="col-sm-4">
                                        <div class="input-group">
                                            <span class="input-group-text"><i
                                                    class="material-icons-outlined fs-5">domain_add</i></span>
                                            <input type="text" name="AnaAlis" class="form-control" id="input49"
                                                placeholder="Bayi Alış Fiyatı" required>
                                        </div>
                                    </div>
                                    <div class="col-sm-5">
                                        <div class="input-group">
                                            <span class="input-group-text"><i
                                                    class="material-icons-outlined fs-5">domain_add</i></span>
                                            <input type="text" class="form-control" name="PaketFiyati" id="input49"
                                                placeholder="Bayi Satış Fiyatı" required>
                                        </div>
                                    </div>
                                </div>

                                <div class="row mb-3">
                                    <label for="input51" class="col-sm-3 col-form-label">Paket Tipi</label>
                                    <div class="col-sm-9">
                                        <div class="input-group">
                                            <span class="input-group-text"><i
                                                    class="material-icons-outlined fs-5">format_list_bulleted</i></span>
                                            <select class="form-select" id="input53" name="PaketTipi">
                                                <option selected="">Tür Seçin</option>
                                                <option value="paket">Paket</option>
                                                <option value="modul">Modul</option>
                                                <option value="yazarkasa">Yazarkasa</option>
                                                <option value="terminal">Terminal</option>
                                                <option value="uygulama">Uygulama</option>
                                            </select>
                                        </div>
                                    </div>
                                </div>
                                <div class="row mb-3">
                                    <label for="input49" class="col-sm-3 col-form-label">Paket Açıklama</label>
                                    <div class="col-sm-9">
                                        <div class="input-group">
                                            <span class="input-group-text"><i
                                                    class="material-icons-outlined fs-5">radio_button_checked</i></span>
                                            <textarea type="text" name="PaketAciklama" class="form-control" id="input49"
                                                placeholder="Açıklama Alanı" rows="5" required></textarea>
                                        </div>
                                    </div>
                                </div>
                                <div class="row mb-3">
                                    <label for="input49" class="col-sm-3 col-form-label">Paket Name</label>
                                    <div class="col-sm-9">
                                        <div class="input-group">
                                            <span class="input-group-text"><i
                                                    class="material-icons-outlined fs-5">radio_button_checked</i></span>
                                            <input type="text" name="PaketName" class="form-control" id="paketname"
                                                placeholder="ÜnPOS" required>
                                        </div>
                                    </div>
                                </div>

                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-light" data-bs-dismiss="modal">İptal</button>
                            <button type="submit" class="btn border-0 text-white" style="background: linear-gradient(135deg, #1a237e 0%, #283593 100%);">İşlem Tamam</button>
                        </div>
                    </div>
                </div>
            </div>
            </form>
            <script>
                // Türkçe karakterleri İngilizce karşılıklarına dönüştürmek için bir fonksiyon
function turkceKarakterDonustur(str) {
  var karakterHaritasi = {
    'ı': 'i', 'İ': 'I', 'ğ': 'g', 'Ğ': 'G',
    'ü': 'u', 'Ü': 'U', 'ş': 's', 'Ş': 'S',
    'ö': 'o', 'Ö': 'O', 'ç': 'c', 'Ç': 'C'
  };
  
  return str.replace(/[ıİğĞüÜşŞöÖçÇ]/g, function(harf) {
    return karakterHaritasi[harf] || harf;
  });
}

// Paketadi input alanına event listener ekle
document.addEventListener('DOMContentLoaded', function() {
  const paketAdiInput = document.getElementById('paketadi');
  const paketNameInput = document.getElementById('paketname');
  
  if (paketAdiInput && paketNameInput) {
    paketAdiInput.addEventListener('input', function() {
      const deger = paketAdiInput.value;
      
      // Türkçe karakterleri İngilizceye çevir
      const ingilizceMetin = turkceKarakterDonustur(deger);
      
      // Küçültüp boşlukları alt çizgi yap
      const sonuc = ingilizceMetin.toLowerCase().replace(/\s+/g, '_');
      
      // Sonucu paketname alanına yaz
      paketNameInput.value = sonuc;
    });
  } else {
    console.error('paketadi veya paketname ID\'li elemanlar bulunamadı!');
  }
});
            </script>
            <!--Model-->
        </div>



        <hr>
        @foreach ($gruplar as $tip => $grup)
        <div class="card mb-3">
            <div class="card-body">
                <h5 class="mb-3">{{ $grup['baslik'] }} <span class="text-muted fs-6">({{ $grup['satirlar']->count() }})</span></h5>
                <div class="table-responsive">
                    <table class="table table-striped table-bordered">
                        <thead>
                            <tr>
                                <th class="col-2">PaketAdi</th>
                                <th class="col-1">Paket Tipi</th>
                                <th class="col-2">Paket Açıklama</th>
                                <th class="col-2">Paket Name</th>
                                <th class="col-1">Alış Fiyatı</th>
                                <th class="col-1">Satış Fiyatı</th>
                                <th class="col-1">İşlemler</th>
                            </tr>
                        </thead>
                        <tbody>
                            @forelse ($grup['satirlar'] as $GeldiBayi)
                                <tr>
                                    <td>{{ $GeldiBayi->PaketAdi }}</td>
                                    <td>
                                        <button type="button"
                                            class="btn btn-success raised d-flex gap-2">{{ $GeldiBayi->PaketTipi }}</button>
                                    </td>
                                    <td>{{ $GeldiBayi->PaketAciklama }}</td>
                                    <td>{{ $GeldiBayi->PaketName }}</td>
                                    <td>{{ $GeldiBayi->AnaAlis }}</td>
                                    <td>{{ $GeldiBayi->PaketFiyati }}</td>
                                    <td>
                                        <div class="col d-flex gap-2">
                                            <form method="POST" action="{{ route('DeleteProgramPackages', ['id' => urlencode(encrypt($GeldiBayi->id))]) }}" onsubmit="return confirm('{{ $GeldiBayi->PaketAdi }} silinsin mi?')">
                                                @csrf
                                                <button type="submit" class="btn btn-danger raised d-flex gap-2">
                                                    <i class="material-icons-outlined">delete</i>
                                                </button>
                                            </form>
                                            <button data-bs-toggle="modal"
                                            data-bs-target="#edit-{{ $GeldiBayi->id }}" type="button" class="btn btn-primary raised d-flex gap-2"><i
                                                    class="material-icons-outlined">edit</i></button>
                                        </div>

                                    </td>
                                </tr>



                                 <!--Model-->
                                <div class="modal fade" id="edit-{{ $GeldiBayi->id }}" tabindex="-1" aria-hidden="true">
                                    <div class="modal-dialog modal-lg ">
                                        <div class="modal-content border-3 border-danger">
                                            <div class="modal-header ">
                                                <h5 class="modal-title"><i class="material-icons-outlined fs-5">domain_add</i> Paket Oluştur</h5>
                                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                                            </div>
                                            <div class="modal-body">
                                                <form method="POST" action="{{ route('UpdateProgramPackages',['id' => urlencode(encrypt($GeldiBayi->id))]) }}" enctype="multipart/form-data">
                                                    @csrf

                                                    <div class="row mb-3">
                                                        <label for="input49" class="col-sm-3 col-form-label">Paket Adı</label>
                                                        <div class="col-sm-9">
                                                            <div class="input-group">
                                                                <span class="input-group-text"><i
                                                                        class="material-icons-outlined fs-5">radio_button_checked</i></span>
                                                                <input type="text" name="PaketAdi" class="form-control" id="paketadi"
                                                                    value="{{ $GeldiBayi->PaketAdi }}" >
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="row mb-3">
                                                        <label for="input49" class="col-sm-3 col-form-label">Alış / Satış Fiyatı</label>
                                                        <div class="col-sm-4">
                                                            <div class="input-group">
                                                                <span class="input-group-text"><i
                                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                                <input type="text" name="AnaAlis" class="form-control" id="input49"
                                                                value="{{ $GeldiBayi->AnaAlis }}">
                                                            </div>
                                                        </div>
                                                        <div class="col-sm-5">
                                                            <div class="input-group">
                                                                <span class="input-group-text"><i
                                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                                <input type="text" class="form-control" name="PaketFiyati" id="input49"
                                                                value="{{ $GeldiBayi->PaketFiyati }}">
                                                            </div>
                                                        </div>
                                                    </div>

                                                    <div class="row mb-3">
                                                        <label for="input51" class="col-sm-3 col-form-label">Paket Tipi</label>
                                                        <div class="col-sm-9">
                                                            <div class="input-group">
                                                                <span class="input-group-text"><i
                                                                        class="material-icons-outlined fs-5">format_list_bulleted</i></span>
                                                                <select class="form-select" id="input53" name="PaketTipi">
                                                                    <option value="{{ $GeldiBayi->PaketTipi }}">{{ $GeldiBayi->PaketTipi }}</option>
                                                                    <option value="paket">Paket</option>
                                                                    <option value="modul">Modul</option>
                                                                    <option value="yazarkasa">Yazarkasa</option>
                                                                    <option value="terminal">Terminal</option>
                                                                    <option value="uygulama">Uygulama</option>
                                                                </select>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="row mb-3">
                                                        <label for="input49" class="col-sm-3 col-form-label">Paket Açıklama</label>
                                                        <div class="col-sm-9">
                                                            <div class="input-group">
                                                                <span class="input-group-text"><i
                                                                        class="material-icons-outlined fs-5">radio_button_checked</i></span>
                                                                <textarea type="text" name="PaketAciklama" class="form-control" id="input49"
                                                                    placeholder="Açıklama Alanı" rows="5" required>{{ $GeldiBayi->PaketAciklama }}</textarea>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="row mb-3">
                                                        <label for="input49" class="col-sm-3 col-form-label">Paket Name</label>
                                                        <div class="col-sm-9">
                                                            <div class="input-group">
                                                                <span class="input-group-text"><i
                                                                        class="material-icons-outlined fs-5">radio_button_checked</i></span>
                                                                <input type="text" name="PaketName" class="form-control" id="paketname"
                                                                value="{{ $GeldiBayi->PaketName }}">
                                                            </div>
                                                        </div>
                                                    </div>

                                            </div>
                                            <div class="modal-footer">
                                                <button type="button" class="btn btn-danger" data-bs-dismiss="modal">İptal</button>
                                                <button type="submit" class="btn btn-primary">İşlem Tamam</button>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                                </form>
                                <!--Model-->
                            @empty
                                <tr>
                                    <td colspan="7" class="text-muted">Bu grupta paket yok.</td>
                                </tr>
                            @endforelse
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
        @endforeach
    </div>
@endsection
