// Download ONNX model from https://github.com/onnx/models/blob/main/vision/body_analysis/arcface/model/arcfaceresnet100-8.onnx
// to project directory before run

using System;
using SixLabors.ImageSharp; // Из одноимённого пакета NuGet
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using SixLabors.ImageSharp.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System.Collections.Generic;

namespace ArcFaceLibrary {
    public class ArcFace
    {
        private InferenceSession session;
        private SemaphoreSlim smph = new SemaphoreSlim(1, 1);
        public ArcFace () {
            using var modelStream = typeof(ArcFace).Assembly.GetManifestResourceStream("arcface-8.onnx");
            using var memoryStream = new MemoryStream();
            if (modelStream != null)
                modelStream.CopyTo(memoryStream);
            this.session = new InferenceSession(memoryStream.ToArray()); 
        }
        public async Task<List<(string, float)>> ProcessAsync2imgs (byte[] img1, byte[] img2, CancellationToken ctn) 
        {
            if (ctn.IsCancellationRequested)
                return new List<(string, float)>();

            var t1 = Task<float[]>.Run (async () => {
                var img1Stream = new MemoryStream(img1);
                var face1 = await Image.LoadAsync<Rgb24>(img1Stream, ctn);

                if (ctn.IsCancellationRequested)
                    return new float[0];

                face1.Mutate(ctx => {
                ctx.Resize(new ResizeOptions 
                            {
                                Size = new Size(112, 112),
                                Mode = ResizeMode.Crop
                            });
                });

                if (ctn.IsCancellationRequested)
                    return new float[0];

                return await GetEmbeddingsAsync(face1, ctn);
            }, ctn);

            var t2 = Task<float[]>.Run (async () => {
                var img2Stream = new MemoryStream(img2);
                var face2 = await Image.LoadAsync<Rgb24>(img2Stream, ctn);

                if (ctn.IsCancellationRequested)
                    return new float[0];

                face2.Mutate(ctx => {
                ctx.Resize(new ResizeOptions 
                            {
                                Size = new Size(112, 112),
                                Mode = ResizeMode.Crop
                            });
                });

                if (ctn.IsCancellationRequested)
                    return new float[0];

                return await GetEmbeddingsAsync(face2, ctn);
            }, ctn);

            var res = await Task.WhenAll(t1, t2);

            if (ctn.IsCancellationRequested)
                return new List<(string, float)>();

            var L = new List<(string, float)>();

            L.Add(("Distance", Distance(res[0], res[1]) * Distance(res[0], res[1])));

            if (ctn.IsCancellationRequested)
                return new List<(string, float)>();

            L.Add(("Similarity", Similarity(res[0], res[1])));
            return L;
        }

        public async Task<Tuple<float, float>[,]> ProcessAsync_NxM (List<byte[]> imgLine1, List<byte[]> imgLine2, CancellationToken ctn) 
        {
            var empty_arr = new Tuple<float, float>[0,0];
            if (imgLine1.Count == 0 || imgLine2.Count == 0)
                return empty_arr;

            var n = imgLine1.Count;
            var m = imgLine2.Count;

            Tuple<float, float>[,] Arr = new Tuple<float, float>[n,m];
            var Line1Vectors = new List<float[]>();
            var Line2Vectors = new List<float[]>();
            Task<float[]>[] LineTasks = new Task<float[]>[n+m];
            for (int i = 0; i < n; i++) {
                int local_i = i;
                if (ctn.IsCancellationRequested)
                    return empty_arr;
                LineTasks[i] = Task<float[]>.Run (async () => {
                    var img1Stream = new MemoryStream(imgLine1[local_i]);
                    var face1 = await Image.LoadAsync<Rgb24>(img1Stream, ctn);

                    if (ctn.IsCancellationRequested)
                        return new float[0];

                    face1.Mutate(ctx => {
                    ctx.Resize(new ResizeOptions 
                                {
                                    Size = new Size(112, 112),
                                    Mode = ResizeMode.Crop
                                });
                    });

                    if (ctn.IsCancellationRequested)
                        return new float[0];

                    return await GetEmbeddingsAsync(face1, ctn);
                }, ctn);
            }
            for (int j = 0; j < m; j++) {
                int local_j = j;
                LineTasks[n+j] = Task<float[]>.Run (async () => {
                    var img2Stream = new MemoryStream(imgLine2[local_j]);
                    var face2 = await Image.LoadAsync<Rgb24>(img2Stream, ctn);
                    if (ctn.IsCancellationRequested)
                        return new float[0];

                    face2.Mutate(ctx => {
                    ctx.Resize(new ResizeOptions 
                                {
                                    Size = new Size(112, 112),
                                    Mode = ResizeMode.Crop
                                });
                    });

                    if (ctn.IsCancellationRequested)
                        return new float[0];

                    return await GetEmbeddingsAsync(face2, ctn);
                }, ctn);
            }
            var res = await Task.WhenAll(LineTasks);

            if (ctn.IsCancellationRequested)
                return empty_arr;
            
            for (int i = 0; i < n; i++) {
                Line1Vectors.Add(res[i]);
            }

            for (int i = n; i < n + m; i++) {
                Line2Vectors.Add(res[i]);
            }

            if (ctn.IsCancellationRequested)
                return empty_arr;

            var t = Task.Run(() => {
                for (int i = 0; i < n; i++) {
                    if (ctn.IsCancellationRequested)
                        return;
                    for (int j = 0; j < m; j++) {
                        Arr[i,j] = new Tuple<float, float>
                        (Distance(Line1Vectors[i], Line2Vectors[j]) * (Distance(Line1Vectors[i], Line2Vectors[j])),
                        Similarity(Line1Vectors[i], Line2Vectors[j]));
                    }
                }
            }, ctn);

            if (ctn.IsCancellationRequested)
                return empty_arr;

            await t;
            return Arr;
        }

        public List<(string, float)> Process2imgs (byte[] img1, byte[] img2) 
        {
            var img1Stream = new MemoryStream(img1);
            var img2Stream = new MemoryStream(img2);

            using var face1 = Image.Load<Rgb24>(img1Stream);
            using var face2 = Image.Load<Rgb24>(img2Stream);

            face1.Mutate(ctx => {
                ctx.Resize(new ResizeOptions 
                            {
                                Size = new Size(112, 112),
                                Mode = ResizeMode.Crop
                            });
            });

            face2.Mutate(ctx => {
                ctx.Resize(new ResizeOptions 
                            {
                                Size = new Size(112, 112),
                                Mode = ResizeMode.Crop
                            });
            });

            var embeddings1 = GetEmbeddings(face1);
            var embeddings2 = GetEmbeddings(face2);

            var L = new List<(string, float)>();
            L.Add(("Distance", Distance(embeddings1, embeddings2) * Distance(embeddings1, embeddings2)));
            L.Add(("Similarity", Similarity(embeddings1, embeddings2)));
            return L;
        }
        float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x*x).Sum());

        float[] Normalize(float[] v) 
        {
            var len = Length(v);
            return v.Select(x => x / len).ToArray();
        }

        float Distance(float[] v1, float[] v2) => Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());

        float Similarity(float[] v1, float[] v2) => v1.Zip(v2).Select(p => p.First * p.Second).Sum();

        DenseTensor<float> ImageToTensor(Image<Rgb24> img)
        {
            var w = img.Width;
            var h = img.Height;
            var t = new DenseTensor<float>(new[] { 1, 3, h, w });

            img.ProcessPixelRows(pa => 
            {
                for (int y = 0; y < h; y++)
                {           
                    Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                    for (int x = 0; x < w; x++)
                    {
                        t[0, 0, y, x] = pixelSpan[x].R;
                        t[0, 1, y, x] = pixelSpan[x].G;
                        t[0, 2, y, x] = pixelSpan[x].B;
                    }
                }
            });
            
            return t;
        }

        async Task<float[]> GetEmbeddingsAsync(Image<Rgb24> face, CancellationToken ctn) 
        {
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("data", ImageToTensor(face)) };

            if (ctn.IsCancellationRequested)
                return new float[0];

            await smph.WaitAsync();
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
            smph.Release();

            if (ctn.IsCancellationRequested)
                return new float[0]; 

            return Normalize(results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray());
        }

        float[] GetEmbeddings(Image<Rgb24> face) 
        {
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("data", ImageToTensor(face)) };

            smph.Wait();
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
            smph.Release();

            return Normalize(results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray());
        }
    }
    
}
