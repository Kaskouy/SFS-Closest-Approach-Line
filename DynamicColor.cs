using System;
using UnityEngine;

public class DynamicColor
{
	public enum E_BLINKING_TYPE
    {
        BLINKING_BINARY,
        BLINKING_REGULAR,
        BLINKING_SMOOTH
    }

    // Pre-computed colors - colors "2" are 10% darker than the original
    public enum E_COLOR
    {
        RED,
        GREEN,
        BLUE,
        YELLOW,
        CYAN,
        MAGENTA,
        LIGHT_RED,
        LIGHT_GREEN,
        LIGHT_BLUE,
        RED_2,
        GREEN_2,
        BLUE_2,
        YELLOW_2,
        CYAN_2,
        MAGENTA_2,
        LIGHT_RED_2,
        LIGHT_GREEN_2,
        LIGHT_BLUE_2
    }

    private float _blinking_period;
    private E_BLINKING_TYPE _blinking_type;
    private Color _dark_color;
    private Color _light_color;
    private float _time;

    public DynamicColor(Color darkColor, Color lightColor, E_BLINKING_TYPE blinkingType, float blinkingPeriod)
    {
        _dark_color = darkColor;
        _light_color = lightColor;
        _blinking_type = blinkingType;
        _blinking_period = Mathf.Max(0.1f, blinkingPeriod);
        _time = Time.time;
    }

    public DynamicColor(E_COLOR color, E_BLINKING_TYPE blinkingType, float blinkingPeriod)
    {
        _dark_color = darkColorsArray[(int)color];
        _light_color = lightColorsArray[(int)color];
        _blinking_type = blinkingType;
        _blinking_period = Mathf.Max(0.1f, blinkingPeriod);
        _time = Time.time;
    }

    public Color getColor()
    {
        float currentTime = Time.time;

        while(currentTime > _time + _blinking_period)
        {
            _time += _blinking_period;
        }

        float currentFractionOfPeriod = Mathf.Clamp01((currentTime - _time) / _blinking_period);

        float colorIntensity = calulateColorIntensity(currentFractionOfPeriod);

        return Color.Lerp(_dark_color, _light_color, colorIntensity);
    }

    private float calulateColorIntensity(float theCurrentFractionOfPeriod)
    {
        float currentFractionOfPeriod = Mathf.Clamp01(theCurrentFractionOfPeriod);

        float colorIntensity = 0.0f;

        switch (_blinking_type)
        {
            case E_BLINKING_TYPE.BLINKING_REGULAR:
                if (currentFractionOfPeriod < 0.5f)
                {
                    colorIntensity = 2.0f * currentFractionOfPeriod;
                }
                else
                {
                    colorIntensity = 2.0f * (1.0f - currentFractionOfPeriod);
                }
                break;

            case E_BLINKING_TYPE.BLINKING_SMOOTH:
                colorIntensity = 4.0f * currentFractionOfPeriod * (1.0f - currentFractionOfPeriod);
                break;

            case E_BLINKING_TYPE.BLINKING_BINARY:
            default:
                if(currentFractionOfPeriod < 0.5f)
                {
                    colorIntensity = 0.0f;
                }
                else
                {
                    colorIntensity = 1.0f;
                }
                break;
        }

        return colorIntensity;
    }

    private Color[] darkColorsArray = { new Color(0.400f, 0.000f, 0.000f),   // RED
                                        new Color(0.000f, 0.400f, 0.000f),   // GREEN
                                        new Color(0.000f, 0.000f, 0.400f),   // BLUE
                                        new Color(0.400f, 0.400f, 0.000f),   // YELLOW
                                        new Color(0.000f, 0.400f, 0.400f),   // CYAN
                                        new Color(0.400f, 0.000f, 0.400f),   // MAGENTA
                                        new Color(0.627f, 0.078f, 0.078f),   // LIGHT RED
                                        new Color(0.118f, 0.471f, 0.275f),   // LIGHT GREEN
                                        new Color(0.118f, 0.196f, 0.471f),   // LIGHT BLUE
                                        new Color(0.360f, 0.000f, 0.000f, 0.6f),   // RED 2
                                        new Color(0.000f, 0.400f, 0.000f, 0.6f),   // GREEN 2
                                        new Color(0.000f, 0.000f, 0.400f, 0.6f),   // BLUE 2
                                        new Color(0.400f, 0.400f, 0.000f, 0.6f),   // YELLOW 2
                                        new Color(0.000f, 0.400f, 0.400f, 0.6f),   // CYAN 2
                                        new Color(0.400f, 0.000f, 0.400f, 0.6f),   // MAGENTA 2
                                        new Color(0.627f, 0.078f, 0.078f, 0.6f),   // LIGHT RED 2
                                        new Color(0.118f, 0.471f, 0.275f, 0.6f),   // LIGHT GREEN 2
                                        new Color(0.118f, 0.196f, 0.471f, 0.6f) }; // LIGHT BLUE 2

    private Color[] lightColorsArray = { new Color(1.000f, 0.000f, 0.000f),   // RED
                                         new Color(0.000f, 1.000f, 0.000f),   // GREEN
                                         new Color(0.000f, 0.000f, 1.000f),   // BLUE
                                         new Color(1.000f, 1.000f, 0.000f),   // YELLOW
                                         new Color(0.000f, 1.000f, 1.000f),   // CYAN
                                         new Color(1.000f, 0.000f, 1.000f),   // MAGENTA
                                         new Color(1.000f, 0.235f, 0.235f),   // LIGHT RED
                                         new Color(0.314f, 1.000f, 0.627f),   // LIGHT GREEN
                                         new Color(0.549f, 0.863f, 1.000f),   // LIGHT BLUE
                                         new Color(1.000f, 0.000f, 0.000f, 0.6f),   // RED 2
                                         new Color(0.000f, 1.000f, 0.000f, 0.6f),   // GREEN 2
                                         new Color(0.000f, 0.000f, 1.000f, 0.6f),   // BLUE 2
                                         new Color(1.000f, 1.000f, 0.000f, 0.6f),   // YELLOW 2
                                         new Color(0.000f, 1.000f, 1.000f, 0.6f),   // CYAN 2
                                         new Color(1.000f, 0.000f, 1.000f, 0.6f),   // MAGENTA 2
                                         new Color(1.000f, 0.235f, 0.235f, 0.6f),   // LIGHT RED 2
                                         new Color(0.314f, 1.000f, 0.627f, 0.6f),   // LIGHT GREEN 2
                                         new Color(0.549f, 0.863f, 1.000f, 0.6f) }; // LIGHT BLUE 2
}
