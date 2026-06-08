using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace BarRestPOS.Utils;

public class EscPosBuilder
{
    private readonly List<byte> _buffer;
    private readonly StringBuilder _textBuffer;
    private int _currentAlignment = 0; // 0=Left, 1=Center, 2=Right

    public EscPosBuilder()
    {
        _buffer = new List<byte>();
        _textBuffer = new StringBuilder();
        Initialize();
    }

    public byte[] GetBytes() => _buffer.ToArray();
    public string GetPlainText() => _textBuffer.ToString();

    public EscPosBuilder Initialize()
    {
        _buffer.AddRange(new byte[] { 0x1B, 0x40 }); // ESC @
        return this;
    }

    public EscPosBuilder PrintLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            _buffer.AddRange(SanitizeToAscii(text));
            
            string alignedText = text;
            if (_currentAlignment == 1)
            {
                int spaces = (48 - text.Length) / 2;
                if (spaces > 0) alignedText = new string(' ', spaces) + text;
            }
            else if (_currentAlignment == 2)
            {
                int spaces = 48 - text.Length;
                if (spaces > 0) alignedText = new string(' ', spaces) + text;
            }
            
            _textBuffer.Append(alignedText);
        }
        _buffer.Add(0x0A); // LF
        _textBuffer.AppendLine();
        return this;
    }

    public EscPosBuilder AlignLeft()
    {
        _currentAlignment = 0;
        _buffer.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
        return this;
    }

    public EscPosBuilder AlignCenter()
    {
        _currentAlignment = 1;
        _buffer.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
        return this;
    }

    public EscPosBuilder AlignRight()
    {
        _currentAlignment = 2;
        _buffer.AddRange(new byte[] { 0x1B, 0x61, 0x02 });
        return this;
    }

    public EscPosBuilder BoldOn()
    {
        _buffer.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
        return this;
    }

    public EscPosBuilder BoldOff()
    {
        _buffer.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        return this;
    }

    public EscPosBuilder NormalFont()
    {
        _buffer.AddRange(new byte[] { 0x1D, 0x21, 0x00 }); // GS ! 0
        return this;
    }

    public EscPosBuilder DoubleSizeFont()
    {
        _buffer.AddRange(new byte[] { 0x1D, 0x21, 0x11 }); // GS ! 11
        return this;
    }

    public EscPosBuilder FeedLines(int lines = 1)
    {
        for (int i = 0; i < lines; i++)
        {
            _buffer.Add(0x0A);
            _textBuffer.AppendLine();
        }
        return this;
    }

    public EscPosBuilder DrawDivider()
    {
        // Impresoras de 80mm suelen caber entre 42 y 48 caracteres normales.
        PrintLine(new string('-', 48));
        return this;
    }

    public EscPosBuilder CutPaper()
    {
        // Cortar papel (GS V A)
        _buffer.AddRange(new byte[] { 0x1D, 0x56, 0x41, 0x10 });
        return this;
    }

    public EscPosBuilder OpenDrawer()
    {
        // ESC p 0 25 255
        _buffer.AddRange(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFF });
        return this;
    }

    public EscPosBuilder PrintColumns(string col1, string col2, int totalWidth = 48)
    {
        // Truncar col1 si es muy larga
        if (col1.Length + col2.Length > totalWidth - 1)
        {
            col1 = col1.Substring(0, Math.Max(0, totalWidth - col2.Length - 2)) + ".";
        }
        int spaces = totalWidth - col1.Length - col2.Length;
        PrintLine(col1 + new string(' ', Math.Max(1, spaces)) + col2);
        return this;
    }
    
    public EscPosBuilder Print3Columns(string col1, string col2, string col3, int w1 = 6, int w2 = 28, int w3 = 14)
    {
        // Alineación: col1 izq, col2 izq, col3 der.
        string c1 = col1.PadRight(w1).Substring(0, w1);
        
        string c2 = col2;
        if (c2.Length > w2) c2 = c2.Substring(0, w2 - 1) + ".";
        c2 = c2.PadRight(w2);
        
        string c3 = col3.PadLeft(w3).Substring(0, w3);
        
        PrintLine(c1 + c2 + c3);
        return this;
    }

    private byte[] SanitizeToAscii(string text)
    {
        // Reemplazar acentos básicos para no romper impresoras configuradas en UTF8/ShiftJIS/PC437 arbitrariamente
        string map = text
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U")
            .Replace("ñ", "n").Replace("Ñ", "N")
            .Replace("¿", "?").Replace("¡", "!");

        return Encoding.ASCII.GetBytes(map);
    }

    public EscPosBuilder PrintImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return this;

        try
        {
            using var image = Image.Load<Rgba32>(imagePath);
            int maxWidth = 384; // estándar para 80mm
            if (image.Width > maxWidth)
            {
                image.Mutate(x => x.Resize(maxWidth, 0));
            }
            int width = image.Width;
            if (width % 8 != 0) width = width + (8 - (width % 8));
            int height = image.Height;

            image.Mutate(x => x.Resize(width, height));
            // Fondo blanco para transparencia
            image.Mutate(x => x.BackgroundColor(Color.White));
            image.Mutate(x => x.Grayscale());

            int bytesWidth = width / 8;
            byte[] header = new byte[] {
                0x1B, 0x61, 0x01, // Align Center
                0x1D, 0x76, 0x30, 0x00, // GS v 0 0
                (byte)(bytesWidth % 256),
                (byte)(bytesWidth / 256),
                (byte)(height % 256),
                (byte)(height / 256)
            };
            
            _buffer.AddRange(header);
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < bytesWidth; x++)
                {
                    byte b = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int px = (x * 8) + bit;
                        if (px < image.Width)
                        {
                            var pixel = image[px, y];
                            int brightness = (pixel.R + pixel.G + pixel.B) / 3;
                            if (pixel.A < 128) brightness = 255;
                            if (brightness < 128)
                            {
                                b |= (byte)(1 << (7 - bit));
                            }
                        }
                    }
                    _buffer.Add(b);
                }
            }
            _buffer.AddRange(new byte[] { 0x1B, 0x61, 0x00 }); // Restore left align
            
            // Simulator placeholder
            _textBuffer.AppendLine("[LOGO DEL NEGOCIO]");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error printing image: {ex.Message}");
        }

        return this;
    }
}
