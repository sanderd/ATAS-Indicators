#if CROSS_PLATFORM

global using CrossColor = System.Drawing.Color;
global using CrossKey = Avalonia.Input.Key;
global using CrossColors = System.Drawing.Color;
global using CrossKeyEventArgs = Avalonia.Input.KeyEventArgs;
global using CrossPen = OFT.Rendering.Tools.CrossPen;
global using CrossBrush = OFT.Rendering.Tools.CrossBrush;

#else

global using CrossColor = System.Windows.Media.Color;
global using CrossKey = System.Windows.Input.Key;
global using CrossColors = System.Windows.Media.Colors;
global using CrossKeyEventArgs = System.Windows.Input.KeyEventArgs;
global using CrossPen = System.Drawing.Pen;
global using CrossBrush = System.Drawing.SolidBrush;

#endif