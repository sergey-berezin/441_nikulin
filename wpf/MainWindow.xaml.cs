using ArcFaceLibrary;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using System.Linq.Expressions;
using System.Collections.Generic;
using static WPFLab.MainWindow;
using Contracts;
using System.Windows.Shapes;
using System.Net.Http.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Security.Policy;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore.Metadata;
using Polly.Retry;
using Polly;

namespace WPFLab
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const int Port = 7074;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private const int RetriesNumber = 5;
        private readonly AsyncRetryPolicy _RetryPolicy;
        public ICommand CancelCalculations { get; private set; }
        public ICommand ClearImages1 { get; private set; }
        public ICommand ClearImages2 { get; private set; }
        public ICommand DeleteImage { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _ProgressBarLevel = 0;
        public int ProgressBarLevel
        {
            get
            {
                return _ProgressBarLevel;
            }
            set
            {
                _ProgressBarLevel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressBarLevel)));
            }
        }

        public enum EComputationStatus
        {
            kNotStarted,
            kCanCancel,
            kCanClearList,
        }
        private EComputationStatus _List1ComputationStatus = 0;
        public EComputationStatus List1ComputationStatus
        {
            get { return _List1ComputationStatus; }
            set
            {
                _List1ComputationStatus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(List1ComputationStatus)));
            }
        }
        private EComputationStatus _List2ComputationStatus = 0;
        public EComputationStatus List2ComputationStatus
        {
            get { return _List2ComputationStatus; }
            set
            {
                _List2ComputationStatus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(List2ComputationStatus)));
            }
        }

        private ObservableCollection<Photo> _PhotosList1 = new ObservableCollection<Photo>();
        private ObservableCollection<Photo> _PhotosList2 = new ObservableCollection<Photo>();

        private bool CanCancel(object sender)
        {
            return List1ComputationStatus == EComputationStatus.kCanCancel || List2ComputationStatus == EComputationStatus.kCanCancel;
        }
        private void DoCancel(object sender)
        {
            cts.Cancel();
        }

        private bool CanClearList1(object sender)
        {
            return List1ComputationStatus == EComputationStatus.kCanClearList;
        }

        private bool CanClearList2(object sender)
        {
            return List2ComputationStatus == EComputationStatus.kCanClearList;
        }
        private void DoClear(object sender, bool isFirstList)
        {
            if (isFirstList)
            {
                _PhotosList1 = new ObservableCollection<Photo>();
                PhotosList1.ItemsSource = _PhotosList1;
                List1ComputationStatus = EComputationStatus.kNotStarted;
            }
            else
            {
                _PhotosList2 = new ObservableCollection<Photo>();
                PhotosList2.ItemsSource = _PhotosList2;
                List2ComputationStatus = EComputationStatus.kNotStarted;
            }
            Similarity.Text = "";
            Distance.Text = "";
            ProgressBarLevel = 0;
        }

        private bool CanDelete(object sender)
        {
            return PhotosList1.SelectedItem != null || PhotosList2.SelectedItem != null;
        }

        private async void DoDelete(object sender)
        {
            try
            {
                var url = $"https://localhost:{Port}/images";
                await _RetryPolicy.ExecuteAsync(async () =>
                {
                    var httpClient = new HttpClient();
                    var res = await httpClient.DeleteAsync(url);
                    res.EnsureSuccessStatusCode();
                    DoClear(sender, true);
                    DoClear(sender, false);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void AddImageToList1(object sender, RoutedEventArgs? e = null)
        {
            await AddImageToList(sender, /*isFirstList=*/true, e);
        }

        private async void AddImageToList2(object sender, RoutedEventArgs? e = null)
        {
            await AddImageToList(sender, /*isFirstList=*/false, e);
        }

        private async Task AddImageToList(object sender, bool isFirstList, RoutedEventArgs? e = null)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Multiselect = true;
                dialog.Filter = "Images (*.jpg, *.png)|*.jpg;*.png";
                dialog.InitialDirectory = System.IO.Path.GetFullPath("../../../../images");
                var response = dialog.ShowDialog();

                if (response == true)
                {
                    if (isFirstList)
                    {
                        List1ComputationStatus = EComputationStatus.kCanCancel;
                    }
                    else
                    {
                        List2ComputationStatus = EComputationStatus.kCanCancel;
                    }
                    ProgressBarLevel = 0;
                    ProgressBar.Maximum = dialog.FileNames.Length;

                    var items = new Photo[dialog.FileNames.Length];

                    for (int i = 0; i < items.Length; ++i)
                    {
                        var path = dialog.FileNames[i];
                        var name = dialog.FileNames[i].Split("\\").Last();
                        items[i] = new Photo { Path = path, Name = name }; 
                        var image = await File.ReadAllBytesAsync(path, cts.Token);
                        items[i].CreateHashCode(image);
                        items[i].Details = new PhotoDetails { Blob = image };
                        items[i].Embeddings = new byte[0];
                    }
                    var Ids = await _RetryPolicy.ExecuteAsync(async () =>
                    {
                        var httpClient = new HttpClient();
                        httpClient.BaseAddress = new Uri($"https://localhost:{Port}/");
                        httpClient.DefaultRequestHeaders.Accept.Clear();
                        httpClient.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));
                        HttpResponseMessage resp = await HttpClientJsonExtensions.PostAsJsonAsync(httpClient, "images", items, cts.Token);
                        resp.EnsureSuccessStatusCode();
                        var Ids = await resp.Content.ReadFromJsonAsync<string[]>();
                        return Ids;
                    });
                    for (int i = 0; i < Ids.Length; i++)
                    {
                        items[i].PhotoId = int.Parse(Ids[i]);
                    }

                    ProgressBarLevel = items.Length;

                    if (isFirstList)
                    {
                        foreach (var item in items)
                        {
                            _PhotosList1.Add(item);
                        }
                        List1ComputationStatus = EComputationStatus.kCanClearList;
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            _PhotosList2.Add(item);
                        }
                        List2ComputationStatus = EComputationStatus.kCanClearList;
                    }
                }
            }
            catch (Exception ex)
            {
                DoClear(this, true);
                DoClear(this, false);
                cts.TryReset();
                cts = new CancellationTokenSource();
                MessageBox.Show(ex.Message);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            PhotosList1.ItemsSource = _PhotosList1;
            PhotosList2.ItemsSource = _PhotosList2;
            PhotosList1.SelectionChanged += PhotosList1OnSelectionChanged;
            PhotosList2.SelectionChanged += PhotosList2OnSelectionChanged;
            CancelCalculations = new RelayCommand(_ =>
            {
                DoCancel(this);
            }, CanCancel
            );
            ClearImages1 = new RelayCommand(_ =>
            {
                DoClear(this, true);
            }, CanClearList1
            );
            ClearImages2 = new RelayCommand(_ =>
            {
                DoClear(this, false);
            }, CanClearList2
            );
            DeleteImage = new RelayCommand(_ =>
            {
                DoDelete(this);
            }, CanDelete
            );
            _RetryPolicy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(RetriesNumber, times =>
                TimeSpan.FromMilliseconds(times * 500));
            DownloadImages();
        }

        private async void UpdateSimilarityAndDistance(Photo item1, Photo item2)
        {
            try
            {
                await _RetryPolicy.ExecuteAsync(async () =>
                {
                    var httpClient = new HttpClient();
                    var url = $"https://localhost:{Port}/compare";
                    var id1 = item1.PhotoId;
                    var id2 = item2.PhotoId;
                    var response = await httpClient.GetAsync($"{url}/{id1}/{id2}");
                    response.EnsureSuccessStatusCode();
                    var result = await response.Content.ReadFromJsonAsync<double[]>();
                    Distance.Text = "        " + string.Format("{0:f5}", result[0]);
                    Similarity.Text = "        " + string.Format("{0:f5}", result[1]);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private int List1SelectedIndex = -1;
        private int List2SelectedIndex = -1;

        public void PhotosList1OnSelectionChanged(object sender, EventArgs e)
        {
            List1SelectedIndex = PhotosList1.SelectedIndex;
            if (List1SelectedIndex != -1 && List2SelectedIndex != -1)
            {
                UpdateSimilarityAndDistance(_PhotosList1[List1SelectedIndex], _PhotosList2[List2SelectedIndex]);
            }
        }

        public void PhotosList2OnSelectionChanged(object sender, EventArgs e)
        {
            List2SelectedIndex = PhotosList2.SelectedIndex;
            if (List1SelectedIndex != -1 && List2SelectedIndex != -1)
            {
                UpdateSimilarityAndDistance(_PhotosList1[List1SelectedIndex], _PhotosList2[List2SelectedIndex]);
            }
        }

        private async void DownloadImages()
        {
            try
            {
                await _RetryPolicy.ExecuteAsync(async () =>
                {
                    var url = $"https://localhost:{Port}/images";
                    var clientHttp = new HttpClient();
                    var response = await clientHttp.GetAsync(url);
                    var photos = await response.Content.ReadFromJsonAsync<Photo[]>();

                    _PhotosList1 = new ObservableCollection<Photo>(photos);
                    PhotosList1.ItemsSource = _PhotosList1;
                    List1ComputationStatus = EComputationStatus.kCanClearList;
                    _PhotosList2 = new ObservableCollection<Photo>(photos);
                    PhotosList2.ItemsSource = _PhotosList2;
                    List2ComputationStatus = EComputationStatus.kCanClearList;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
