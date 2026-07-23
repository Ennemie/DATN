namespace DATN.UI
{
    /// <summary>
    /// Marker interface. Concrete VMs hold bindable data; Views bind to this only.
    /// </summary>
    public interface IViewModel
    {
        // TODO: Add INotifyPropertyChanged or reactive property support (e.g., UniRx)
        void OnBind();
        void OnUnbind();
    }
}
