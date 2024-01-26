using MultiFunPlayer.Input;
using System.ComponentModel;
using System.Text;

namespace MultiFunPlayer.Shortcut;

[DisplayName("Axis Drive")]
internal sealed class AxisDriveShortcut(IShortcutActionResolver actionResolver, IAxisInputGestureDescriptor gesture)
    : AbstractShortcut<IAxisInputGesture, IAxisInputGestureData>(actionResolver, gesture)
{
    public AxisDriveShortcutMode DriveMode { get; set; } = AxisDriveShortcutMode.Relative;
    public bool Invert { get; set; } = false;

    protected override void Update(IAxisInputGesture gesture)
    {
        AxisInputGestureData Relative() => AxisInputGestureData.FromGestureRelative(gesture, invertDelta: Invert);
        AxisInputGestureData Absolute() => AxisInputGestureData.FromGestureAbsolute(gesture, invertValue: Invert);

        Invoke(DriveMode switch
        {
            AxisDriveShortcutMode.Absolute => Absolute(),
            AxisDriveShortcutMode.Relative => Relative(),
            AxisDriveShortcutMode.RelativeNegativeOnly when gesture.Delta < 0 => Relative(),
            AxisDriveShortcutMode.RelativePositiveOnly when gesture.Delta > 0 => Relative(),
            AxisDriveShortcutMode.RelativeJoystick when gesture.Value < 0.5 && gesture.Delta < 0 => Relative(),
            AxisDriveShortcutMode.RelativeJoystick when gesture.Value > 0.5 && gesture.Delta > 0 => Relative(),
            _ => null
        });
    }

    protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        PrintProperty(builder, () => DriveMode);
        PrintProperty(builder, () => Invert);
    }
}
internal enum AxisDriveShortcutMode
{
    Absolute,
    Relative,
    RelativePositiveOnly,
    RelativeNegativeOnly,
    RelativeJoystick
}