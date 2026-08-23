using OpenCvSharp;
using Rect = OpenCvSharp.Rect; // Resolve a ambiguidade com Tesseract.Rect

namespace LabelWise.Infrastructure.Helpers;

public static class CardCroppingHelper
{

    /// <summary>
    /// Recorta especificamente a caixa de arte (ilustração principal) da carta frontal.
    /// </summary>
    public static byte[] GenerateArtBoxZoom(byte[] frontStraightBytes)
    {
        using var frontMat = Cv2.ImDecode(frontStraightBytes, ImreadModes.Color);
        if (frontMat.Empty()) return null;

        // Proporções aproximadas da Art Box no Pokémon TCG
        int x = (int)(frontMat.Width * 0.08);       // Pula a margem esquerda
        int y = (int)(frontMat.Height * 0.12);      // Pula o topo (nome, HP)
        int width = (int)(frontMat.Width * 0.84);   // Pega a largura da arte
        int height = (int)(frontMat.Height * 0.42); // Pega a altura da arte

        // Garante que o retângulo não ultrapasse a imagem
        var artBoxRect = new Rect(x, y, width, height);

        using var artBoxMat = new Mat(frontMat, artBoxRect);
        return artBoxMat.ToBytes(".jpg");
    }

    /// <summary>
    /// Recorta os 4 cantos da Frente e do Verso e gera uma única imagem composta em Grid (2x4).
    /// </summary>
    public static byte[] GenerateCornersZoomGrid(byte[] frontStraightBytes, byte[] backStraightBytes)
    {
        using var frontMat = Cv2.ImDecode(frontStraightBytes, ImreadModes.Color);
        using var backMat = Cv2.ImDecode(backStraightBytes, ImreadModes.Color);

        if (frontMat.Empty() || backMat.Empty()) return null;

        var frontCorners = Crop4Corners(frontMat);
        var backCorners = Crop4Corners(backMat);

        // Combina os 4 cantos da frente e os 4 do verso em um grid 2x4 (8 sub-imagens)
        using var rowFront = CombineHorizontally(frontCorners);
        using var rowBack = CombineHorizontally(backCorners);
        using var combinedGrid = CombineVertically(new[] { rowFront, rowBack });

        // Descarta as sub-matrizes da memória
        foreach (var mat in frontCorners.Concat(backCorners)) mat.Dispose();

        return combinedGrid.ToBytes(".jpg");
    }

    /// <summary>
    /// Recorta as 4 bordas (Top, Bottom, Left, Right) da Frente e Verso e gera uma imagem composta em tiras.
    /// </summary>
    public static byte[] GenerateEdgesZoomGrid(byte[] frontStraightBytes, byte[] backStraightBytes)
    {
        using var frontMat = Cv2.ImDecode(frontStraightBytes, ImreadModes.Color);
        using var backMat = Cv2.ImDecode(backStraightBytes, ImreadModes.Color);

        if (frontMat.Empty() || backMat.Empty()) return null;

        var frontEdges = Crop4Edges(frontMat);
        var backEdges = Crop4Edges(backMat);

        using var rowFront = CombineHorizontally(frontEdges);
        using var rowBack = CombineHorizontally(backEdges);
        using var combinedGrid = CombineVertically(new[] { rowFront, rowBack });

        foreach (var mat in frontEdges.Concat(backEdges)) mat.Dispose();

        return combinedGrid.ToBytes(".jpg");
    }

    private static Mat[] Crop4Corners(Mat src)
    {
        int cropW = (int)(src.Width * 0.20);  // 20% da largura
        int cropH = (int)(src.Height * 0.15); // 15% da altura

        return new Mat[]
        {
            new Mat(src, new Rect(0, 0, cropW, cropH)),                                  // Canto Sup. Esquerdo
            new Mat(src, new Rect(src.Width - cropW, 0, cropW, cropH)),                  // Canto Sup. Direito
            new Mat(src, new Rect(0, src.Height - cropH, cropW, cropH)),                 // Canto Inf. Esquerdo
            new Mat(src, new Rect(src.Width - cropW, src.Height - cropH, cropW, cropH))  // Canto Inf. Direito
        };
    }

    private static Mat[] Crop4Edges(Mat src)
    {
        int marginW = (int)(src.Width * 0.12);
        int marginH = (int)(src.Height * 0.08);

        return new Mat[]
        {
            new Mat(src, new Rect(0, 0, src.Width, marginH)),                            // Borda Superior
            new Mat(src, new Rect(0, src.Height - marginH, src.Width, marginH)),          // Borda Inferior
            new Mat(src, new Rect(0, 0, marginW, src.Height)),                            // Borda Esquerda
            new Mat(src, new Rect(src.Width - marginW, 0, marginW, src.Height))           // Borda Direita
        };
    }

    private static Mat CombineHorizontally(Mat[] images)
    {
        int targetHeight = images.Min(img => img.Height);
        var resized = images.Select(img =>
        {
            var dst = new Mat();
            double scale = (double)targetHeight / img.Height;
            Cv2.Resize(img, dst, new Size((int)(img.Width * scale), targetHeight));
            return dst;
        }).ToArray();

        var result = new Mat();
        Cv2.HConcat(resized, result);
        foreach (var m in resized) m.Dispose();
        return result;
    }

    private static Mat CombineVertically(Mat[] images)
    {
        int targetWidth = images.Min(img => img.Width);
        var resized = images.Select(img =>
        {
            var dst = new Mat();
            double scale = (double)targetWidth / img.Width;
            Cv2.Resize(img, dst, new Size(targetWidth, (int)(img.Height * scale)));
            return dst;
        }).ToArray();

        var result = new Mat();
        Cv2.VConcat(resized, result);
        foreach (var m in resized) m.Dispose();
        return result;
    }
}