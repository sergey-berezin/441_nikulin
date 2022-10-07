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
            modelStream.CopyTo(memoryStream);
            this.session = new InferenceSession(memoryStream.ToArray()); 
        }
        public async Task<List<(string, double)>> ProcessAsync (byte[] img1, byte[] img2, CancellationTokenSource cts) 
        {
            var img1Stream = new MemoryStream(img1);
            var img2Stream = new MemoryStream(img2);

            if (Cancelled(cts))
                return new List<(string, double)>();

            var t1 = Task<float[]>.Run (async () => {
                var img1Stream = new MemoryStream(img1);
                var face1 = await Image.LoadAsync<Rgb24>(img1Stream, cts.Token);

                if (Cancelled(cts))
                    return new float[0];

                face1.Mutate(ctx => {
                ctx.Resize(new ResizeOptions 
                            {
                                Size = new Size(112, 112),
                                Mode = ResizeMode.Crop
                            });
                });

                if (Cancelled(cts))
                    return new float[0];

                return GetEmbeddings(face1, cts);
            }, cts.Token);

            var t2 = Task<float[]>.Run (async () => {
                var img2Stream = new MemoryStream(img2);
                var face2 = await Image.LoadAsync<Rgb24>(img2Stream, cts.Token);

                if (Cancelled(cts))
                    return new float[0];

                face2.Mutate(ctx => {
                ctx.Resize(new ResizeOptions 
                            {
                                Size = new Size(112, 112),
                                Mode = ResizeMode.Crop
                            });
                });

                if (Cancelled(cts))
                    return new float[0];

                return GetEmbeddings(face2, cts);
            }, cts.Token);

            var res = await Task.WhenAll(t1, t2);

            if (Cancelled(cts))
                return new List<(string, double)>();

            var L = new List<(string, double)>();

            L.Add(("Distance", Distance(res[0], res[1]) * Distance(res[0], res[1])));

            if (Cancelled(cts))
                return new List<(string, double)>();

            L.Add(("Similarity", Similarity(res[0], res[1])));
            return L;
        }

        public List<(string, double)> Process (byte[] img1, byte[] img2) 
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

            var L = new List<(string, double)>();
            L.Add(("Distance", Distance(embeddings1, embeddings2) * Distance(embeddings1, embeddings2)));
            L.Add(("Similarity", Similarity(embeddings1, embeddings2)));
            return L;
        }
        bool Cancelled (CancellationTokenSource cts) {
            if (cts.IsCancellationRequested)
                return true;
            return false;
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

        float[] GetEmbeddings(Image<Rgb24> face, CancellationTokenSource? cts = null) 
        {
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("data", ImageToTensor(face)) };

            if (cts!=null && Cancelled(cts)) 
                return new float[0];

            smph.WaitAsync();
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
            smph.Release();

            if (cts!=null && Cancelled(cts)) 
                return new float[0];

            return Normalize(results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray());
        }
    }
    
}