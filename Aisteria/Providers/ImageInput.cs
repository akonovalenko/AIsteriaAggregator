namespace Aisteria.Providers
{
    /// <summary>One image to send to a vision-capable provider.</summary>
    public sealed class ImageInput
    {
        public byte[] Data { get; set; }
        public string Mime { get; set; }

        public ImageInput() { }
        public ImageInput(byte[] data, string mime) { Data = data; Mime = mime; }
    }
}
