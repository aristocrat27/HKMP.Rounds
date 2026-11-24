using System;
using System.Collections.Generic;
using HKMP.Rounds.Settings;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal static class MapPresetPreviewLoader
    {
        private static readonly Dictionary<int, Sprite> FallbackSprites = new Dictionary<int, Sprite>();

        public static Sprite GetFallbackSprite(MapPresetDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            Sprite cached;
            if (FallbackSprites.TryGetValue(definition.Id, out cached) && cached != null)
            {
                return cached;
            }

            const int width = 512;
            const int height = 216;
            Color32 baseColor = MapPresetCatalog.GetFallbackColor(definition);
            Color32[] pixels = new Color32[width * height];
            System.Random random = new System.Random(definition.Id * 7919 + 31);
            int[] lightsX = new int[24];
            int[] lightsY = new int[24];
            int[] lightsRadius = new int[24];

            for (int i = 0; i < lightsX.Length; i++)
            {
                lightsX[i] = random.Next(0, width);
                lightsY[i] = random.Next(height / 3, height - 8);
                lightsRadius[i] = random.Next(1, 4);
            }

            for (int y = 0; y < height; y++)
            {
                float vertical = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float wave =
                        Mathf.Sin(x * 0.018f + definition.Id * 0.71f) * 8f +
                        Mathf.Sin(x * 0.047f + definition.Id * 1.37f) * 4f;
                    float horizon = height * (0.17f + 0.035f * Mathf.Sin(x * 0.014f + definition.Id));
                    bool foreground = y < horizon + wave;
                    float light = 0.48f + vertical * 0.48f;
                    float noise = Mathf.Sin((x + definition.Id * 13) * 0.113f) *
                                  Mathf.Sin((y + definition.Id * 7) * 0.091f) * 0.08f;
                    if (foreground)
                    {
                        light *= 0.43f;
                    }

                    light = Mathf.Clamp(light + noise, 0.16f, 1.05f);
                    Color32 color = new Color32(
                        ToByte(baseColor.r * light),
                        ToByte(baseColor.g * light),
                        ToByte(baseColor.b * light),
                        255);

                    if (definition.Id == 2)
                    {
                        float crystalWave = Mathf.Abs(Mathf.Sin(x * 0.034f + definition.Id * 2.3f));
                        int crystalTop = (int)(height * (0.22f + crystalWave * 0.2f));
                        if (x % 71 < 13 && y > crystalTop && y < height * 0.76f)
                        {
                            color = Blend(color, new Color32(190, 151, 235, 255), 0.72f);
                        }
                    }

                    pixels[y * width + x] = color;
                }
            }

            Color32 lightColor = new Color32(225, 232, 199, 255);
            for (int lightIndex = 0; lightIndex < lightsX.Length; lightIndex++)
            {
                int centerX = lightsX[lightIndex];
                int centerY = lightsY[lightIndex];
                int radius = lightsRadius[lightIndex];
                for (int y = Math.Max(0, centerY - radius); y <= Math.Min(height - 1, centerY + radius); y++)
                {
                    for (int x = Math.Max(0, centerX - radius); x <= Math.Min(width - 1, centerX + radius); x++)
                    {
                        int dx = x - centerX;
                        int dy = y - centerY;
                        int distanceSquared = dx * dx + dy * dy;
                        if (distanceSquared <= radius * radius)
                        {
                            int pixelIndex = y * width + x;
                            pixels[pixelIndex] = Blend(
                                pixels[pixelIndex],
                                lightColor,
                                distanceSquared == 0 ? 0.95f : 0.55f);
                        }
                    }
                }
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.name = "HKMP.Rounds.MapPresetFallback." + definition.Id;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite fallback = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f);
            fallback.name = "HKMP.Rounds.MapPresetFallback." + definition.Id;
            FallbackSprites[definition.Id] = fallback;
            return fallback;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        }

        private static Color32 Blend(Color32 first, Color32 second, float amount)
        {
            amount = Mathf.Clamp01(amount);
            return new Color32(
                ToByte(Mathf.Lerp(first.r, second.r, amount)),
                ToByte(Mathf.Lerp(first.g, second.g, amount)),
                ToByte(Mathf.Lerp(first.b, second.b, amount)),
                255);
        }
    }
}
