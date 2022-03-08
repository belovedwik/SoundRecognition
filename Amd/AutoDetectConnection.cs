using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeltaM.DeltaTell.Amd
{ 
    public class AutodetectConnection : INotifyPropertyChanged
    {
        public enum AutoDetectMetod
        {
            Normal = 0,
            Hard = 1,
            Partial = 2,
            Partial75 = 3
        }

        public bool? Enabled { get; set; }
        public AutoDetectMetod Metod { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
