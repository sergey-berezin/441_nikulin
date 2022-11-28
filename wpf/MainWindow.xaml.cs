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
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;
using System.Collections.Generic;
using static WPFLab.MainWindow;

namespace WPFLab
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ArcFace MLModel = new ArcFace();
        private SemaphoreSlim GetPhotosSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource cts = new CancellationTokenSource();
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

        public class PhotosListItem
        {
            [Key]
            public int PhotoId { get; set; }
            public string Name { get; set; }
            public string Path { get; set; }
            public int ImageHash { get; set; }
            public PhotoDetails Details { get; set; }
            public byte[] Embeddings { get; set; }
            public void CreateHashCode(byte[] image)
            {
                ImageHash = image.Length;
                foreach (int value in image)
                {
                    ImageHash = unchecked(ImageHash * 226817 + value);
                }
            }
        }

        private byte[] FloatsToBytes(float[] array)
        {
            var byteBuffer = new byte[array.Length * 4];
            Buffer.BlockCopy(array, 0, byteBuffer, 0, byteBuffer.Length);
            return byteBuffer;
        }

        private float[] BytesToFloats(byte[] bytes)
        {
            var floatBuffer = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, floatBuffer, 0, bytes.Length);
            return floatBuffer;
        }

        public class PhotoDetails
        {
            [Key]
            [ForeignKey(nameof(PhotosListItem))]
            public int PhotoId { get; set; }
            public byte[] Blob { get; set; }
        }

        public class LibraryContext : DbContext
        {
            public DbSet<PhotosListItem> Photos { get; set; }
            public DbSet<PhotoDetails> Details { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder o) =>
                o.UseSqlite("Data Source=arcface.db");
        }
        private SemaphoreSlim dbSemaphore = new SemaphoreSlim(1, 1);

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

        private bool CanDelete(object sender)
        {
            return PhotosList1.SelectedItem != null || PhotosList2.SelectedItem != null;
        }

        private async void DoDelete(object sender)
        {
            bool isFirstList;
            PhotosListItem? item = null;
            if (PhotosList1.SelectedIndex != -1)
            {
                isFirstList = true;
                item = PhotosList1.SelectedItem as PhotosListItem;
            }
            else if (PhotosList2.SelectedIndex != -1)
            {
                isFirstList = false;
                item = PhotosList2.SelectedItem as PhotosListItem;
            }
            if (item == null)
            {
                MessageBox.Show("No photo is selected to delete");
                return;
            }
            try
            {
                await dbSemaphore.WaitAsync();
                using (var db = new LibraryContext())
                {
                    var photo = db.Photos.Where(record => record.PhotoId == item.PhotoId).Include(record => record.Details).First();
                    if (photo == null)
                    {
                        return;
                    }
                    db.Details.Remove(photo.Details);
                    db.Photos.Remove(photo);
                    await db.SaveChangesAsync();
                    var result1 = _PhotosList1.Where(record => record.PhotoId == item.PhotoId).ToList();
                    if (result1.Count() > 0)
                    {
                        _PhotosList1.Remove(result1[0]);
                        if (_PhotosList1.Count() == 0)
                        {
                            List1ComputationStatus = EComputationStatus.kNotStarted;
                        }
                    }
                    var result2 = _PhotosList2.Where(record => record.PhotoId == item.PhotoId).ToList();
                    if (result2.Count() > 0)
                    {
                        _PhotosList2.Remove(result2[0]);
                        if (_PhotosList2.Count() == 0)
                        {
                            List2ComputationStatus = EComputationStatus.kNotStarted;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                dbSemaphore.Release();
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

        private async Task<PhotosListItem?> ProcessImage(string path, bool isFirstList)
        {
            var pathSplit = path.Split("\\");
            var name = pathSplit.Last();
            var item = new PhotosListItem { Name = name, Path = path };
            var image = await File.ReadAllBytesAsync(path, cts.Token);
            item.CreateHashCode(image);

            await dbSemaphore.WaitAsync();
            using (var db = new LibraryContext())
            {
                item.Details = new PhotoDetails { Blob = image };
                var maybePhoto = await db.Photos.Where(record => record.ImageHash == item.ImageHash).Take(1).
                                            Include(record => record.Details).ToListAsync();
                if (maybePhoto.Count == 1) // DB may contain the image
                {
                    if (Enumerable.SequenceEqual(item.Details.Blob, maybePhoto[0].Details.Blob)) // DB contains the image
                    {
                        item.Embeddings = maybePhoto[0].Embeddings;
                        dbSemaphore.Release();
                        return null;
                    }
                }

                var floatEmbeddings = await MLModel.ProcessImage(image, cts.Token);
                item.Embeddings = FloatsToBytes(floatEmbeddings);
                await db.Photos.AddAsync(item);
                await db.SaveChangesAsync();
                dbSemaphore.Release();
                return item;
            }
        }

        private async Task AddImageToList(object sender, bool isFirstList, RoutedEventArgs? e = null)
        {
            try
            {
                await GetPhotosSemaphore.WaitAsync();
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

                    var tasks = new Task[dialog.FileNames.Length];
                    for (int i = 0; i < tasks.Length; ++i)
                    {
                        var path = dialog.FileNames[i];
                        tasks[i] = Task.Run(async () =>
                        {
                            var result = await ProcessImage(path, isFirstList);
                            return result;
                        }).ContinueWith(task =>
                        {
                            var result = task.Result;
                            ++ProgressBarLevel;
                            if (result != null)
                            {
                                if (isFirstList)
                                {
                                    _PhotosList1.Add(result);
                                }
                                else
                                {
                                    _PhotosList2.Add(result);
                                }
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    await Task.WhenAll(tasks);

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
                if (dbSemaphore.CurrentCount == 0)
                {
                    dbSemaphore.Release();
                }
                DoClear(this, true);
                DoClear(this, false);
                cts.TryReset();
                cts = new CancellationTokenSource();
                MessageBox.Show(ex.Message);
            }
            finally
            {
                GetPhotosSemaphore.Release();
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
            DownloadImagesFromDb();
        }

        private void UpdateSimilarityAndDistance(PhotosListItem item1, PhotosListItem item2)
        {
            var embeddings1 = BytesToFloats(item1.Embeddings);
            var embeddings2 = BytesToFloats(item2.Embeddings);
            Similarity.Text = MLModel.Similarity(embeddings1, embeddings2).ToString();
            Distance.Text = MLModel.Distance(embeddings1, embeddings2).ToString();
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

        private void DownloadImagesFromDb()
        {
            using (var db = new LibraryContext())
            {

                var photos = db.Photos.Include(item => item.Details).ToList();
                _PhotosList1 = new ObservableCollection<PhotosListItem>(photos);
                PhotosList1.ItemsSource = _PhotosList1;
                List1ComputationStatus = EComputationStatus.kCanClearList;
                _PhotosList2 = new ObservableCollection<PhotosListItem>(photos);
                PhotosList2.ItemsSource = _PhotosList2;
                List2ComputationStatus = EComputationStatus.kCanClearList;
            }
        }
    }
}
