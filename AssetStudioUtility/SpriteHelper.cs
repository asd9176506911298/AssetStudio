using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace AssetStudio
{
    public enum SpriteMaskMode
    {
        Off,
        On,
        MaskOnly,
        Export
    }

    public static class SpriteHelper
    {
        public static SpriteInfo spriteinfo = new SpriteInfo();
        public static bool drawSpriteEdge = false;
        public static DrawingOptions options;

        public static string GetSpriteFolderName(this Sprite m_Sprite)
        {
            return Regex.Replace(m_Sprite.m_Name, @"\d+$", "");
        }

        public static Image<Bgra32> GetSpriteTexture(this Sprite m_Sprite, ref string texture2dName)
        {
            if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
            {
                if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.texture.TryGet(out var m_Texture2D))
                {
                    texture2dName = m_Texture2D.Name;
                    return m_Texture2D.ConvertToImage(true);
                }
            }
            return null;
        }

        public static Image<Bgra32> GetImage(this Sprite m_Sprite, SpriteMaskMode spriteMaskMode = SpriteMaskMode.On)
        {
            if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
            {
                if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.texture.TryGet(out var m_Texture2D))
                {
                    return CutImage(m_Sprite, m_Texture2D, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.downscaleMultiplier, spriteAtlasData.settingsRaw);
                }
            }
            else
            {
                if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D) && m_Sprite.m_RD.alphaTexture.TryGet(out var m_AlphaTexture2D) && spriteMaskMode != SpriteMaskMode.Off)
                {
                    Image<Bgra32> tex = null;
                    if (spriteMaskMode != SpriteMaskMode.MaskOnly)
                    {
                        tex = CutImage(m_Sprite, m_Texture2D, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, m_Sprite.m_RD.settingsRaw);
                    }
                    var alphaTex = CutImage(m_Sprite, m_AlphaTexture2D, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, m_Sprite.m_RD.settingsRaw);

                    switch (spriteMaskMode)
                    {
                        case SpriteMaskMode.On:
                            tex.ApplyRGBMask(alphaTex, isPreview: true);
                            return tex;
                        case SpriteMaskMode.Export:
                            tex.ApplyRGBMask(alphaTex);
                            return tex;
                        case SpriteMaskMode.MaskOnly:
                            return alphaTex;
                    }
                }
                else if (m_Sprite.m_RD.texture.TryGet(out m_Texture2D))
                {
                    return CutImage(m_Sprite, m_Texture2D, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, m_Sprite.m_RD.settingsRaw);
                }
            }
            return null;
        }

        private static void ApplyRGBMask(this Image<Bgra32> tex, Image<Bgra32> texMask, bool isPreview = false)
        {
            using (texMask)
            {
                if (tex.Width != texMask.Width || tex.Height != texMask.Height)
                {
                    var resampler = isPreview ? KnownResamplers.NearestNeighbor : KnownResamplers.Bicubic;
                    texMask.Mutate(x => x.Resize(tex.Width, tex.Height, resampler));
                }

                tex.ProcessPixelRows(texMask, (sourceTex, targetTexMask) =>
                {
                    for (int y = 0; y < texMask.Height; y++)
                    {
                        var texRow = sourceTex.GetRowSpan(y);
                        var maskRow = targetTexMask.GetRowSpan(y);
                        for (int x = 0; x < maskRow.Length; x++)
                        {
                            var grayscale = (byte)((maskRow[x].R + maskRow[x].G + maskRow[x].B) / 3);
                            texRow[x].A = grayscale;
                        }
                    }
                });
            }
        }

        private static Image<Bgra32> CutImage(Sprite m_Sprite, Texture2D m_Texture2D, Rectf textureRect, Vector2 textureRectOffset, float downscaleMultiplier, SpriteSettings settingsRaw)
        {
            var originalImage = m_Texture2D.ConvertToImage(false);
            if (originalImage != null)
            {
                if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
                {
                    var width = (int)(m_Texture2D.m_Width / downscaleMultiplier);
                    var height = (int)(m_Texture2D.m_Height / downscaleMultiplier);
                    originalImage.Mutate(x => x.Resize(width, height));
                }
                var rectX = (int)MathF.Floor(textureRect.x);
                var rectY = (int)MathF.Floor(textureRect.y);
                var rectRight = (int)MathF.Floor(textureRect.x + textureRect.width); // 修改：從 MathF.Ceiling 改為 MathF.Floor
                var rectBottom = (int)MathF.Floor(textureRect.y + textureRect.height); // 修改：從 MathF.Ceiling 改為 MathF.Floor
                rectRight = Math.Min(rectRight, originalImage.Width);
                rectBottom = Math.Min(rectBottom, originalImage.Height);
                var rect = new Rectangle(rectX, rectY, rectRight - rectX, rectBottom - rectY);
                var spriteImage = originalImage.Clone(x => x.Crop(rect));
                var canvasWidth = (int)m_Sprite.m_Rect.width;
                var canvasHeight = (int)m_Sprite.m_Rect.height;
                var unifiedImage = new Image<Bgra32>(canvasWidth, canvasHeight, SixLabors.ImageSharp.Color.Transparent);
                Vector2 pivotPosition = new Vector2(
                      m_Sprite.m_Rect.width * m_Sprite.m_Pivot.X + m_Sprite.m_Offset.X,
                      m_Sprite.m_Rect.height * m_Sprite.m_Pivot.Y + m_Sprite.m_Offset.Y
                  );
                Vector2 adjustedPivot = pivotPosition - textureRectOffset;
                var placeX = (int)Math.Round((canvasWidth * 0.5f - adjustedPivot.X));

                originalImage.Dispose();
                if (settingsRaw.packed == 1)
                {
                    //RotateAndFlip
                    switch (settingsRaw.packingRotation)
                    {
                        case SpritePackingRotation.FlipHorizontal:
                            spriteImage.Mutate(x => x.Flip(FlipMode.Horizontal));
                            break;
                        case SpritePackingRotation.FlipVertical:
                            spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                            break;
                        case SpritePackingRotation.Rotate180:
                            spriteImage.Mutate(x => x.Rotate(180));
                            break;
                        case SpritePackingRotation.Rotate90:
                            spriteImage.Mutate(x => x.Rotate(270));
                            Logger.Warning($"Please Tell Author Yuki.kaco Rotate90 {m_Texture2D.Name} {m_Sprite.Name} ");
                            break;
                    }
                }

                //Tight
                if (settingsRaw.packingMode == SpritePackingMode.Tight)
                {
                    try
                    {
                        var matrix = Matrix3x2.CreateScale(m_Sprite.m_PixelsToUnits);
                        matrix *= Matrix3x2.CreateTranslation(m_Sprite.m_Rect.width * m_Sprite.m_Pivot.X - textureRectOffset.X, m_Sprite.m_Rect.height * m_Sprite.m_Pivot.Y - textureRectOffset.Y);
                        var triangles = GetTriangles(m_Sprite.m_RD);
                        var points = triangles.Select(x => x.Select(y => new PointF(y.X, y.Y)).ToArray());
                        var pathBuilder = new PathBuilder(matrix);
                        foreach (var p in points)
                        {
                            pathBuilder.AddLines(p);
                            pathBuilder.CloseFigure();
                        }
                        var path = pathBuilder.Build();
                        var options = new DrawingOptions
                        {
                            GraphicsOptions = new GraphicsOptions
                            {
                                Antialias = false,
                                AlphaCompositionMode = drawSpriteEdge
                                    ? PixelAlphaCompositionMode.SrcOver   // Red Edge
                                    : PixelAlphaCompositionMode.DestOut   // No Red Edge
                            }
                        };
                        if (triangles.Length < 1024)
                        {
                            var rectP = new RectangularPolygon(0, 0, rect.Width, rect.Height);
                            try
                            {
                                spriteImage.Mutate(x => x.Fill(options, SixLabors.ImageSharp.Color.Red, rectP.Clip(path)));
                                unifiedImage.Mutate(x => x.DrawImage(spriteImage, new Point(placeX, (int)(Math.Round(-adjustedPivot.Y + pivotPosition.Y))), 1f));


                                //// 計算矩形邊界（假設 rectP 與 spriteImage 的範圍對應）
                                //int leftX = placeX -1; // 外框左邊
                                //int rightX = placeX + spriteImage.Width; // 外框右邊
                                //int bottomY = (int)(-adjustedPivot.Y + pivotPosition.Y) -1; // 外框底邊
                                //int topY = (int)(-adjustedPivot.Y + pivotPosition.Y) + spriteImage.Height; // 外框頂邊

                                //// 水平邊（上下兩條線）
                                //for (int x = leftX; x <= rightX; x++)
                                //{
                                //    if (x >= 0 && x < canvasWidth)
                                //    {
                                //        if (bottomY >= 0 && bottomY < canvasHeight)
                                //            unifiedImage[x, bottomY] = new Bgra32(255, 0, 0, 255); // 底邊外框
                                //        if (topY >= 0 && topY < canvasHeight)
                                //            unifiedImage[x, topY] = new Bgra32(255, 0, 0, 255); // 頂邊外框
                                //    }
                                //}

                                //// 垂直邊（左右兩條線）
                                //for (int y = bottomY; y <= topY; y++)
                                //{
                                //    if (y >= 0 && y < canvasHeight)
                                //    {
                                //        if (leftX >= 0 && leftX < canvasWidth)
                                //            unifiedImage[leftX, y] = new Bgra32(255, 0, 0, 255); // 左邊外框
                                //        if (rightX >= 0 && rightX < canvasWidth)
                                //            unifiedImage[rightX, y] = new Bgra32(255, 0, 0, 255); // 右邊外框
                                //    }
                                //}

                                unifiedImage.Mutate(x => x.Flip(FlipMode.Vertical));
                                string bundleName = System.IO.Path.GetFileNameWithoutExtension(m_Sprite.assetsFile.originalPath).Replace(".spriteatlas", "");
                                spriteinfo.Add(0, rect.X, rect.Y,placeX,(int)(-adjustedPivot.Y + pivotPosition.Y),rect.Width,rect.Height, string.Join("_", m_Texture2D.Name.Split(System.IO.Path.GetInvalidFileNameChars())), $"{bundleName}/{GetSpriteFolderName(m_Sprite)}/{m_Sprite.Name}.png", false, settingsRaw.packingRotation);

                                var pointss = triangles.SelectMany(x => x).Select(y => new PointF(y.X, y.Y)).ToArray();
                                spriteinfo.SavePathPoints(spriteinfo.sid.Count - 1, pointss);
                                return unifiedImage;
                            }
                            catch (ArgumentOutOfRangeException ex)
                            {
                                // ignored
                                Logger.Warning($"{ex}");
                            }
                        }
                        using (var mask = new Image<Bgra32>(rect.Width, rect.Height, SixLabors.ImageSharp.Color.Black))
                        {
                            mask.Mutate(x => x.Fill(options, SixLabors.ImageSharp.Color.Red, path));
                            var brush = new ImageBrush(mask);
                            spriteImage.Mutate(x => x.Fill(options, brush));
                            spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                            return spriteImage;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Warning($"{m_Sprite.m_Name} Unable to render the packed sprite correctly.\n{e}");
                    }
                }

                //Rectangle
                spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                return spriteImage;
            }

            return null;
        }

        private static Vector2[][] GetTriangles(SpriteRenderData m_RD)
        {
            if (m_RD.vertices != null) //5.6 down
            {
                var vertices = m_RD.vertices.Select(x => (Vector2)x.pos).ToArray();
                var triangleCount = m_RD.indices.Length / 3;
                var triangles = new Vector2[triangleCount][];
                for (int i = 0; i < triangleCount; i++)
                {
                    var first = m_RD.indices[i * 3];
                    var second = m_RD.indices[i * 3 + 1];
                    var third = m_RD.indices[i * 3 + 2];
                    var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                    triangles[i] = triangle;
                }
                return triangles;
            }
            else //5.6 and up
            {
                var triangles = new List<Vector2[]>();
                var m_VertexData = m_RD.m_VertexData;
                var m_Channel = m_VertexData.m_Channels[0]; //kShaderChannelVertex
                var m_Stream = m_VertexData.m_Streams[m_Channel.stream];
                using (var vertexReader = new BinaryReader(new MemoryStream(m_VertexData.m_DataSize)))
                {
                    using (var indexReader = new BinaryReader(new MemoryStream(m_RD.m_IndexBuffer)))
                    {
                        foreach (var subMesh in m_RD.m_SubMeshes)
                        {
                            vertexReader.BaseStream.Position = m_Stream.offset + subMesh.firstVertex * m_Stream.stride + m_Channel.offset;

                            var vertices = new Vector2[subMesh.vertexCount];
                            for (int v = 0; v < subMesh.vertexCount; v++)
                            {
                                vertices[v] = vertexReader.ReadVector3();
                                vertexReader.BaseStream.Position += m_Stream.stride - 12;
                            }

                            indexReader.BaseStream.Position = subMesh.firstByte;

                            var triangleCount = subMesh.indexCount / 3u;
                            for (int i = 0; i < triangleCount; i++)
                            {
                                var first = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var second = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var third = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                                triangles.Add(triangle);
                            }
                        }
                    }
                }
                return triangles.ToArray();
            }
        }
    }
}
