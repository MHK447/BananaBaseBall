// Taps for the Colosseum battle (see ColosseumHaptics.cs).
#import <UIKit/UIKit.h>

extern "C" {

// style: 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid. strength: 0 to 1.
void ColosseumHapticImpact(int style, float strength)
{
    if (style < 0 || style > 4) style = 1;
    UIImpactFeedbackGenerator *tap = [[UIImpactFeedbackGenerator alloc] initWithStyle:(UIImpactFeedbackStyle)style];
    if (@available(iOS 13.0, *)) {
        if (strength < 0.05f) strength = 0.05f;
        if (strength > 1.0f) strength = 1.0f;
        [tap impactOccurredWithIntensity:strength];
    } else {
        [tap impactOccurred];
    }
}

// kind: 0 success, 1 warning, 2 error.
void ColosseumHapticNotify(int kind)
{
    if (kind < 0 || kind > 2) kind = 0;
    UINotificationFeedbackGenerator *note = [[UINotificationFeedbackGenerator alloc] init];
    [note notificationOccurred:(UINotificationFeedbackType)kind];
}

}
