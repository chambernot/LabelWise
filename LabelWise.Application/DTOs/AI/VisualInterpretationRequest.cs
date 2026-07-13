namespace LabelWise.Application.DTOs.AI
{
    /// <summary>
    /// Represents the request for a visual interpretation of an image.
    /// </summary>
    public class VisualInterpretationRequest
    {
        /// <summary>
        /// The local file path of the image to be interpreted.
        /// </summary>
        public string ImagePath { get; set; }
    }
}
