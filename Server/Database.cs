using Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using ArcFaceLibrary;

namespace Server.Database
{
    public class LibraryContext : DbContext
    {
        public DbSet<Photo> Photos { get; set; }
        public DbSet<PhotoDetails> Details { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder o) =>
            o.UseSqlite("Data Source=arcface.db");
    }
    public interface IImagesDB
    {
        public Task<string[]> AddImages(Photo[] images, CancellationToken ct);
        public Task<IEnumerable<Photo>> GetImages(CancellationToken ct);
        public Task DeleteImages(CancellationToken ct);
        public Task<ComparisonResult> CompareImages(string firstId, string secondId, CancellationToken ct);
    }
    public class ImagesDB : IImagesDB
    {
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
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

        private ArcFace MLModel = new ArcFace();
        public async Task<string[]> AddImages(Photo[] images, CancellationToken ct)
        {
            var idArr = new List<string>();
            try
            {
                await semaphore.WaitAsync();
                using (var db = new LibraryContext())
                {
                    foreach (var image in images)
                    {
                        var maybePhotos = await db.Photos.Where(record => record.ImageHash == image.ImageHash)
                                        .Include(record => record.Details).ToListAsync();
                        if (maybePhotos.Count >= 1) // DB may contain the image
                        {
                            if (maybePhotos.Any(photo => 
                                Enumerable.SequenceEqual(image.Details.Blob, photo.Details.Blob)))
                            {
                                var foundPhoto = maybePhotos.Where(photo =>
                                Enumerable.SequenceEqual(image.Details.Blob, photo.Details.Blob))
                                    .FirstOrDefault();
                                var ss = foundPhoto.PhotoId.ToString();
                                idArr.Add(foundPhoto.PhotoId.ToString());
                                continue;
                            }
                        }

                        var embedding = await MLModel.ProcessImage(image.Details.Blob, ct);
                        image.Embeddings = FloatsToBytes(embedding);
                        db.Photos.Add(image);
                        await db.SaveChangesAsync();
                        var Id = db.Photos.Where(item => item.PhotoId == db.Photos.Max(item => item.PhotoId))
                                .First().PhotoId;
                        idArr.Add(Id.ToString());
                    }
                }
                return idArr.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                semaphore.Release();
            }
            
        }

        public async Task<ComparisonResult> CompareImages(string firstId, string secondId, CancellationToken ct)
        {
            try
            {
                var result = new ComparisonResult();

                await semaphore.WaitAsync();
                using var db = new LibraryContext();
                var embeddings1 = BytesToFloats(db.Photos.First(photo => photo.PhotoId.ToString() == firstId).Embeddings);
                var embeddings2 = BytesToFloats(db.Photos.First(photo => photo.PhotoId.ToString() == secondId).Embeddings);
                result.similarity = MLModel.Similarity(embeddings1, embeddings2);
                result.distance = MLModel.Distance(embeddings1, embeddings2);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task DeleteImages(CancellationToken ct)
        {
            try
            {
                await semaphore.WaitAsync();
                using var db = new LibraryContext();
                var Photos = db.Photos.Include(photo => photo.Details);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [Details]");
                await db.Database.ExecuteSqlRawAsync("DELETE FROM [Photos]");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<IEnumerable<Photo>> GetImages(CancellationToken ct)
        {
            try
            {
                await semaphore.WaitAsync();
                using var db = new LibraryContext();
                return db.Photos.Include(photo => photo.Details).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
