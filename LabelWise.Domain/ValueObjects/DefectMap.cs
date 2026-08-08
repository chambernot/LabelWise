using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.ValueObjects;

public record DefectMap(
    string DefectType, // Ex: "Whitening", "Scratch"
    float X,
    float Y,
    float Width,
    float Height
);
