using Application.Abstractions.Messaging;
using Domain.Cars.Atribbutes;

namespace Application.Cars.Upload;

public sealed class UploadImageCarCommand : ICommand<Guid>
{
     public Guid CarId { get; set; }
    public byte[] ImageData { get; set; }
    public string FileName { get; set; }
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
}
 