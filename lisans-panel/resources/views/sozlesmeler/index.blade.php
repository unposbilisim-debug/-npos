@extends('layouts.app')

@section('content')
    <div class="main-content">
        <!--breadcrumb-->
        <div class="page-breadcrumb d-none d-sm-flex align-items-center mb-3">
            <div class="breadcrumb-title pe-3">ÜnPOS CRM</div>
            <div class="ps-3">
                <nav aria-label="breadcrumb">
                    <ol class="breadcrumb mb-0 p-0">

                        <li class="breadcrumb-item active" aria-current="page">{{ $sayfaBaslik ?? 'Sözleşme İşlemleri' }}</li>
                    </ol>
                </nav>
            </div>
            <div class="ms-auto">
                                                <button type="button" class="btn border-0 text-white" style="background: linear-gradient(135deg, #1a237e 0%, #283593 100%);" data-bs-toggle="modal" data-bs-target="#exampleLargeModal">{{ ($sayfaBaslik ?? 'Sözleşme') }} Oluştur</button>

            </div>
            <!--Model-->
            <div class="modal fade" id="exampleLargeModal" tabindex="-1" aria-hidden="true">
                <div class="modal-dialog modal-lg ">
                    <div class="modal-content border-3" style="border-color: #1a237e !important;">
                        <div class="modal-header ">
                            <h5 class="modal-title"><i class="material-icons-outlined fs-5">domain_add</i> Sözleşme Oluştur
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            @if (Auth::user()->role == 'agent')
                                <form method="POST" action="{{ route('AddAgreement') }}" enctype="multipart/form-data">
                            @endif
                                @if (Auth::user()->role == 'admin')
                                    <form method="POST" action="{{ route('AddAgreement') }}" enctype="multipart/form-data">
                                @endif
                                    @csrf
                                    <div class="row mb-3">
                                        <label for="input49" class="col-sm-3 col-form-label">Müşteri</label>
                                        <div class="col-sm-9">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">format_list_bulleted</i></span>
                                                <select class="form-select" id="input53" name="musteri">
                                                    <option selected="">Müşteri Seçin</option>
                                                    @foreach ($Musteriler as $GeldiMusteri)
                                                        @if (Auth::user()->role == 'agent')
                                                            @if ($GeldiMusteri->bayi == Auth::user()->id)
                                                                <option value="{{ $GeldiMusteri->id }}">{{ $GeldiMusteri->Unvan }}
                                                                </option>
                                                            @endif
                                                        @endif
                                                        <option value="{{ $GeldiMusteri->id }}">{{ $GeldiMusteri->Unvan }}
                                                        </option>
                                                    @endforeach
                                                </select>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="row mb-3">
                                        <label for="input49" class="col-sm-3 col-form-label">Sözleşme Tipi</label>
                                        <div class="col-sm-4">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">format_list_bulleted</i></span>
                                                @if (!empty($aktifTip))
                                                    <input type="hidden" name="tipi" value="{{ $aktifTip }}">
                                                    <input type="text" class="form-control" value="{{ $sayfaBaslik }}" readonly>
                                                @else
                                                <select class="form-select" id="input53" name="tipi">
                                                    <option selected="">Lütfen Seçim Yapın</option>
                                                    <option value="3">Satış sözleşmesi</option>
                                                    <option value="1">Bakım sözleşmesi</option>
                                                    <option value="4">Servis sözleşmesi</option>
                                                </select>
                                                @endif
                                            </div>
                                        </div>
                                        <div class="col-sm-5">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                <input type="text" class="form-control" name="destekbedeli" id="input49"
                                                    placeholder="Destek Bedeli" required>
                                            </div>
                                        </div>
                                    </div>

                                    <div class="row mb-3">
                                        <label for="input49" class="col-sm-3 col-form-label">Kullanici Sayisi</label>
                                        <div class="col-sm-4">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                <input type="text" class="form-control" name="kullanicisayisi" id="input49"
                                                    placeholder="Kullanici Sayisi" required>
                                            </div>
                                        </div>
                                        <div class="col-sm-5">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                <input type="text" class="form-control" name="surum" id="input49"
                                                    placeholder="Sürüm" required>
                                            </div>
                                        </div>
                                    </div>

                                    <div class="row mb-3">
                                        <label for="input49" class="col-sm-3 col-form-label">Ödeme Şekli</label>
                                        <div class="col-sm-4">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                <input type="text" class="form-control" name="odemesekli" id="input49"
                                                    placeholder="Ödeme Şekli" required>
                                            </div>
                                        </div>
                                        <div class="col-sm-5">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">format_list_bulleted</i></span>
                                                <select class="form-select" id="input53" name="paket">
                                                    <option selected="">Lütfen Seçim Yapın</option>
                                                    @foreach ($Paket as $GeldiPaket)
                                                        <option value="{{ $GeldiPaket->PaketAdi }}">{{ $GeldiPaket->PaketAdi }}
                                                        </option>
                                                    @endforeach
                                                </select>
                                            </div>
                                        </div>
                                    </div>

                                    <div class="row mb-3">
                                        <label for="input49" class="col-sm-3 col-form-label">Bitiş Tarihi</label>
                                        <div class="col-sm-4">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                <input type="date" class="form-control" name="bitistarihi" id="input49"
                                                    placeholder="Bitiş Tarihi" required>
                                            </div>
                                        </div>
                                        <div class="col-sm-5">
                                            <div class="input-group">
                                                <span class="input-group-text"><i
                                                        class="material-icons-outlined fs-5">domain_add</i></span>
                                                <input type="file" class="form-control" name="imzalanmisevrak" id="input49"
                                                    placeholder="imzalanmisevrak" required>
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
            <!--Model-->
        </div>



        <hr>
        <div class="card">
            <div class="card-body">
                <div class="table-responsive">
                    <table id="example2" class="table table-striped table-bordered">
                        <thead>
                            <tr>
                                @if (Auth::user()->role == 'admin')
                                    <th class="col-1">Bayi</th>
                                @endif
                                <th>Müşteri</th>
                                <th class="col-2">Tipi</th>
                                <th class="col-1">Sürüm</th>
                                <th class="col-1">Bitiş Tarihi</th>
                                <th class="col-1">İşlemler</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach ($Sozlesmeler as $GeldiBayi)
                                <tr>
                                    </td>
                                    @if (Auth::user()->role == 'admin')
                                        <td>{{ $GeldiBayi->bayiKullanici->name }}</td>
                                    @endif
                                    <td>{{ $GeldiBayi->musteriKaydi ? $GeldiBayi->musteriKaydi->Unvan : 'Müşteri Silinmiş' }}</td>
                                    <td>
                                        @if ($GeldiBayi->tipi == '1')
                                            <span class="btn btn-primary raised d-flex gap-2">Bakım sözleşmesi</span>
                                        @elseif ($GeldiBayi->tipi == '2')
                                            <span class="btn btn-success raised d-flex gap-2">Eğitim sözleşmesi</span>
                                        @elseif ($GeldiBayi->tipi == '3')
                                            <span class="btn btn-info raised d-flex gap-2">Satış sözleşmesi</span>
                                        @elseif ($GeldiBayi->tipi == '4')
                                            <span class="btn btn-warning raised d-flex gap-2">Servis sözleşmesi</span>
                                        @endif
                                    </td>
                                    <td>
                                        <a type="button" class="btn btn-danger raised d-flex gap-2">{{ $GeldiBayi->surum }}</a>
                                    </td>
                                    <td>
                                        <a type="button"
                                            class="btn btn-success raised d-flex gap-2">{{ $GeldiBayi->bitistarihi }}</a>
                                    </td>
                                    <td>
                                        <div class="col d-flex gap-2">
                                            @if (Auth::user()->role == 'agent')
                                                <a href="{{ route('DownloadAgreement', $GeldiBayi->id) }}"
                                                    type="button" class="btn btn-info raised d-flex gap-2"><i
                                                        class="material-icons-outlined">download</i></a>
                                                        <button type="button" 
                                                        class="btn btn-primary raised d-flex gap-2" 
                                                        disabled>
                                                   <i class="material-icons-outlined">edit</i>
                                                </button>
                                            @endif
                                            @if (Auth::user()->role == 'admin')
                                                <a href="{{ route('DownloadAgreement', $GeldiBayi->id) }}"
                                                    type="button" class="btn btn-info raised d-flex gap-2"><i
                                                        class="material-icons-outlined">download</i></a>
                                                        <button type="button" 
                                                        class="btn btn-primary raised d-flex gap-2" 
                                                        disabled>
                                                   <i class="material-icons-outlined">edit</i>
                                                </button>
                                            @endif
                                        </div>

                                    </td>
                                </tr>
                            @endforeach
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    </div>
@endsection