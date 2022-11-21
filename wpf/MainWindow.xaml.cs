using ArcFaceLibrary;
using EmotionsWPF;
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

namespace WPFLab
{
    class PhotosListItem
    {
        public PhotosListItem(string otherName, string otherPath)
        {
            Name = otherName;
            Path = otherPath;
        }
        public string Name { get; set; }
        public string Path { get; set; }
        public float[]? Embeddings { get; set; }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ArcFace MLModel = new ArcFace();
        private CancellationTokenSource cts = new CancellationTokenSource();
        public ICommand CancelCalculations { get; private set; }
        public ICommand ClearImages1 { get; private set; }
        public ICommand ClearImages2 { get; private set; }
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

        private ObservableCollection<PhotosListItem> _PhotosList1 = new ObservableCollection<PhotosListItem>();
        private ObservableCollection<PhotosListItem> _PhotosList2 = new ObservableCollection<PhotosListItem>();

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
                _PhotosList1 = new ObservableCollection<PhotosListItem>();
                PhotosList1.ItemsSource = _PhotosList1;
                List1ComputationStatus = EComputationStatus.kNotStarted;
            }
            else
            {
                _PhotosList2 = new ObservableCollection<PhotosListItem>();
                PhotosList2.ItemsSource = _PhotosList2;
                List2ComputationStatus = EComputationStatus.kNotStarted;
            }
            ProgressBarLevel = 0;
        }

        private async void GetPhotosList1(object sender, RoutedEventArgs? e = null)
        {
            await GetPhotosList(sender, /*isFirstList=*/true, e);
        }

        private async void GetPhotosList2(object sender, RoutedEventArgs? e = null)
        {
            await GetPhotosList(sender, /*isFirstList=*/false, e);
        }

        private async Task ProcessImage(string path, bool isFirstList)
        {
            var pathSplit = path.Split("\\");
            var name = pathSplit.Last();

            var item = new PhotosListItem(name, path);
            if (isFirstList)
            {
                _PhotosList1.Add(item);
            }
            else
            {
                _PhotosList2.Add(item);
            }

            var image = await File.ReadAllBytesAsync(path, cts.Token);
            item.Embeddings = await MLModel.ProcessImage(image, cts.Token);
        }
        private async Task GetPhotosList(object sender, bool isFirstList, RoutedEventArgs? e = null)
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

                    foreach (var path in dialog.FileNames)
                    {
                        await ProcessImage(path, isFirstList);
                        ++ProgressBarLevel;
                    }
                    if (isFirstList)
                    {
                        List1ComputationStatus = EComputationStatus.kCanClearList;
                    }
                    else
                    {
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
        }

        private void UpdateSimilarityAndDistance(PhotosListItem item1, PhotosListItem item2)
        {
            Similarity.Text = MLModel.Similarity(item1.Embeddings, item2.Embeddings).ToString();
            Distance.Text = MLModel.Distance(item1.Embeddings, item2.Embeddings).ToString();
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
    }
}
