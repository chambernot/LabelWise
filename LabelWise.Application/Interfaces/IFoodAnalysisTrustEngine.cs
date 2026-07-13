using LabelWise.Application.DTOs.FoodAnalysisTrust;

namespace LabelWise.Application.Interfaces;

public interface IFoodAnalysisTrustEngine
{
    FoodAnalysisTrustReport Evaluate(FoodAnalysisTrustInput input);
}
