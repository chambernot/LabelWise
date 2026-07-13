using System.Threading.Tasks;
using LabelWise.Application.DTOs.AI;

namespace LabelWise.Application.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that provides visual interpretation of images.
    /// </summary>
    public interface IVisualInterpreter
    {
        /// <summary>
        /// Interprets the visual content of an image to extract product-related information.
        /// </summary>
        /// <param name="request">The request containing the image path.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the interpretation result.</returns>
        Task<VisualInterpretationResult> InterpretImageAsync(VisualInterpretationRequest request);
    }
}
