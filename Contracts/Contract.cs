using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contracts
{
    public class Photo
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

    public class PhotoDetails
    {
        [Key]
        [ForeignKey(nameof(Photo))]
        public int PhotoId { get; set; }
        public byte[] Blob { get; set; }
    }

    public class ComparisonResult
    {
        public double distance;
        public double similarity;
    }
}
