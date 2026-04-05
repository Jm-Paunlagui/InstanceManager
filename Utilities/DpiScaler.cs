using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    /// <summary>
    /// Provides DPI-aware scaling helpers for the application.
    /// Computes a combined scale factor from the system DPI and the user's font size preference.
    /// All layout constants (pixel sizes, positions) should be multiplied by Scale() to adapt
    /// to high-DPI displays and user font size preferences.
    /// </summary>
    public static class DpiScaler
    {
        // Base DPI that the designer layout was created at (96 DPI = 100% scaling)
        private const float BaseDpi = 96f;

        // Cached system DPI (set once at startup, does not change per-monitor on .NET 4.0)
        private static float _systemDpi = 96f;
        private static bool _initialized;

        // User font size multiplier (1.0 = default, 1.25 = 125%, etc.)
        private static float _fontSizeMultiplier = 1.0f;

        // Minimum font size in points that GDI+ accepts
        private const float MinFontSize = 1.0f;

        /// <summary>
        /// Gets the current system DPI value.
        /// </summary>
        public static float SystemDpi { get { return _systemDpi; } }

        /// <summary>
        /// Gets the combined scale factor (system DPI factor * user font size multiplier).
        /// At 96 DPI with font size 100%, this returns 1.0.
        /// At 144 DPI with font size 125%, this returns 1.875 (1.5 * 1.25).
        /// </summary>
        public static float ScaleFactor
        {
            get { return (_systemDpi / BaseDpi) * _fontSizeMultiplier; }
        }

        /// <summary>
        /// Gets the DPI scale factor only (without font size multiplier).
        /// Useful for sizing images/icons that should scale with DPI but not font size.
        /// </summary>
        public static float DpiScaleFactor
        {
            get { return _systemDpi / BaseDpi; }
        }

        /// <summary>
        /// Gets the user font size multiplier (1.0 = 100%).
        /// </summary>
        public static float FontSizeMultiplier
        {
            get { return _fontSizeMultiplier; }
        }

        /// <summary>
        /// Initializes the DPI scaler by reading the system DPI.
        /// Should be called once at application startup after Application.EnableVisualStyles().
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // Use GetDeviceCaps to query the system DPI (works on all Windows versions)
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    _systemDpi = GetDeviceCaps(hdc, LOGPIXELSX);
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }
            catch
            {
                _systemDpi = 96f;
            }
        }

        /// <summary>
        /// Sets the user font size multiplier from the settings.
        /// fontSizePercent is a value like 100, 125, 150, etc.
        /// </summary>
        public static void SetFontSizePercent(int fontSizePercent)
        {
            _fontSizeMultiplier = Math.Max(75, Math.Min(200, fontSizePercent)) / 100f;
        }

        /// <summary>
        /// Scales a design-time pixel value by the combined DPI and font size factor.
        /// </summary>
        public static int Scale(int value)
        {
            return (int)Math.Round(value * ScaleFactor);
        }

        /// <summary>
        /// Scales a design-time pixel value by the combined DPI and font size factor.
        /// </summary>
        public static float Scale(float value)
        {
            return value * ScaleFactor;
        }

        /// <summary>
        /// Returns a scaled Size from design-time width and height.
        /// </summary>
        public static Size ScaleSize(int width, int height)
        {
            return new Size(Scale(width), Scale(height));
        }

        /// <summary>
        /// Returns a scaled Point from design-time x and y.
        /// </summary>
        public static Point ScalePoint(int x, int y)
        {
            return new Point(Scale(x), Scale(y));
        }

        /// <summary>
        /// Creates a scaled font based on the base font size and the user font size multiplier.
        /// DPI scaling is handled by WinForms auto-scaling, so only the user multiplier is applied.
        /// Falls back to a system font if the requested font family is not available.
        /// </summary>
        public static Font CreateFont(string familyName, float baseSizeInPoints, FontStyle style)
        {
            float scaledSize = Math.Max(baseSizeInPoints * _fontSizeMultiplier, MinFontSize);

            try
            {
                return new Font(familyName, scaledSize, style);
            }
            catch
            {
                // Font family not available or invalid parameters — fall back to system font
                try
                {
                    return new Font(SystemFonts.DefaultFont.FontFamily, scaledSize, style);
                }
                catch
                {
                    // Absolute fallback — return the default font unscaled
                    return new Font(FontFamily.GenericSansSerif, Math.Max(baseSizeInPoints, MinFontSize), style);
                }
            }
        }

        /// <summary>
        /// Creates a scaled font based on the base font size and the user font size multiplier.
        /// </summary>
        public static Font CreateFont(string familyName, float baseSizeInPoints)
        {
            return CreateFont(familyName, baseSizeInPoints, FontStyle.Regular);
        }

        /// <summary>
        /// Applies DPI and font-size aware scaling to a form and all its children.
        /// Call this in the form's constructor or Load event after InitializeComponent().
        /// This applies the user font size multiplier to the form's font, which cascades to
        /// all children that inherit the form font.
        /// </summary>
        public static void ScaleForm(Form form)
        {
            if (form == null) return;

            // Apply font size multiplier to the form's font, which cascades to all children
            if (_fontSizeMultiplier != 1.0f)
            {
                float baseFontSize = form.Font.Size;
                float scaledSize = Math.Max(baseFontSize * _fontSizeMultiplier, MinFontSize);

                try
                {
                    form.Font = new Font(form.Font.FontFamily, scaledSize, form.Font.Style);
                }
                catch
                {
                    // Font creation failed (e.g. custom font not installed) — fall back to system font
                    try
                    {
                        form.Font = new Font(SystemFonts.DefaultFont.FontFamily, scaledSize, form.Font.Style);
                    }
                    catch
                    {
                        // Absolute fallback — use generic sans serif at base size
                        try
                        {
                            form.Font = new Font(FontFamily.GenericSansSerif, Math.Max(baseFontSize, MinFontSize), form.Font.Style);
                        }
                        catch
                        {
                            // Unable to set any font — leave the form's existing font unchanged
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively scales all controls in a container by the font size multiplier.
        /// Useful for programmatically created dialogs that don't use designer auto-scaling.
        /// </summary>
        public static void ScaleControl(Control control)
        {
            if (control == null) return;
            if (_fontSizeMultiplier == 1.0f) return;

            // Scale the control's own font if it has one explicitly set
            if (control.Font != null)
            {
                float scaledSize = Math.Max(control.Font.Size * _fontSizeMultiplier, MinFontSize);
                try
                {
                    control.Font = new Font(control.Font.FontFamily, scaledSize, control.Font.Style);
                }
                catch
                {
                    // Font creation failed — leave the control's existing font unchanged
                }
            }
        }

        // Win32 GDI constants and imports for DPI detection
        private const int LOGPIXELSX = 88;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
    }
}
