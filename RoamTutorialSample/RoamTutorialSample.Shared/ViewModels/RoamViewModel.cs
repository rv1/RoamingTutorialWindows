using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace RoamTutorialSample.ViewModels
{
    class BindableBase : INotifyPropertyChanged
    {
        protected bool SetProperty<T>(ref T storage, T value,
          [CallerMemberName] String propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            storage = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(
          [CallerMemberName] string propertyName = null)
        {
            var eventHandler = this.PropertyChanged;
            if (eventHandler != null)
            {
                eventHandler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }


    class RoamViewModel : BindableBase
    {
        public string SelectedValue
        {
            get { return (this._selectedValue); }
            set { base.SetProperty(ref this._selectedValue, value); }
        }

        string _selectedValue;

        IEnumerable<string> images;

        public IEnumerable<string> Images
        {
            get
            {
                if (this.images == null)
                {
                    GetImagesAsync();
                }
                return (images);
            }
        }

        async Task GetImagesAsync()
        {
            // might want to cache this list perhaps.  
            var folder = await Package.Current.InstalledLocation.GetFolderAsync("Images");

            var files = await folder.GetFilesAsync();
            this.images = files
              .Where(f => System.IO.Path.GetExtension(f.Path) == @".png")
              .Select(f => f.Path)
              .ToList();

            this.OnPropertyChanged("Images");
        }
    }
}