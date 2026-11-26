using System.Drawing.Drawing2D;

namespace RobRunPotato;

// A six-step slider with mouse, keyboard and focus support.
internal sealed class PresetSlider : Control
{
    private static readonly string[] Labels = { "СТАНДАРТ", "ОПТИМАЛЬНО", "НИЗКО", "ОЧЕНЬ НИЗКО", "POTATO", "УЛЬТРА" };
    private static readonly Color Red = Color.FromArgb(240, 43, 65);
    private int _value;
    public event EventHandler? ValueChanged;
    public int Minimum => 0;
    public int Maximum => Labels.Length - 1;
    public int Value
    {
        get => _value;
        set
        {
            int next = Math.Clamp(value, Minimum, Maximum);
            if (_value == next) return;
            _value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public PresetSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleName = "Уровень графики";
        AccessibleDescription = "Выберите один из шести пресетов. Используйте стрелки влево и вправо.";
        AccessibleRole = AccessibleRole.Slider;
        Height = 66;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float left = 55, right = Math.Max(left + 1, Width - 55), y = 15;
        float step = (right - left) / Maximum;
        using var inactive = new Pen(Color.FromArgb(69, 64, 69), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var active = new Pen(Red, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(inactive, left, y, right, y);
        if (Value > 0) g.DrawLine(active, left, y, left + step * Value, y);
        using var dot = new SolidBrush(Color.FromArgb(98, 92, 98));
        using var selected = new SolidBrush(Red);
        using var white = new SolidBrush(Color.White);
        for (int i = 0; i <= Maximum; i++)
        {
            float x = left + step * i;
            g.FillEllipse(i == Value ? selected : dot, x - 3, 29, 6, 6);
            var rect = new Rectangle((int)(x - step / 2), 42, (int)step + 1, 20);
            TextRenderer.DrawText(g, Labels[i], Font, rect, i == Value ? Red : Color.FromArgb(160, 154, 160),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
        float thumb = left + step * Value;
        g.FillEllipse(selected, thumb - 10, y - 10, 20, 20);
        g.FillEllipse(white, thumb - 7, y - 7, 14, 14);
        if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(1, 1, Width - 2, Height - 2), ForeColor, BackColor);
    }

    private void SetFromMouse(int x)
    {
        float width = Math.Max(1, Width - 110);
        Value = (int)Math.Round((x - 55) / width * Maximum);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus(); Capture = true; SetFromMouse(e.X);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture && e.Button == MouseButtons.Left) SetFromMouse(e.X);
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: Value--; break;
            case Keys.Right: Value++; break;
            case Keys.Home: Value = Minimum; break;
            case Keys.End: Value = Maximum; break;
            default: return;
        }
        e.Handled = true;
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
