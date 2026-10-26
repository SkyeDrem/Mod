using System;

namespace FXDSP.Core
{
    public sealed class TransferSelection
    {
        public int Quantity { get; private set; } = 10;
        public int Step { get; private set; } = TransferAmounts.SliderSteps;
        private bool _initialized;
        private int _maximum;
        public void SetInput(int quantity, int maximum)
        { Quantity = Math.Max(0, quantity); if (maximum > 0) Step = TransferAmounts.ToSlider(Quantity, maximum); _maximum = maximum; _initialized = true; }
        public void SetSlider(int step, int maximum)
        { Step = Math.Max(0, Math.Min(TransferAmounts.SliderSteps, step)); Quantity = TransferAmounts.FromSlider(Step, maximum); _maximum = maximum; _initialized = true; }
        public void Refresh(int maximum, bool contextChanged)
        {
            if (_initialized && contextChanged) Quantity = TransferAmounts.FromSlider(Step, maximum);
            else if (maximum > 0 && (!_initialized || maximum != _maximum)) Step = TransferAmounts.ToSlider(Quantity, maximum);
            _maximum = maximum; _initialized = true;
        }
    }
}
