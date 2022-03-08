using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeltaM.DeltaTell.Amd
{ 
    public class AutodetectConnection : INotifyPropertyChanged
    {
        public bool? Enabled { get; set; }
        public AutoDetectMethod Metod { get; set; }
        public bool? IsNeedTrim { get; set; }
        public int NeedTrimValue { get; set; }
        public SilenceDetectMethod SilenceDetectMethod { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
