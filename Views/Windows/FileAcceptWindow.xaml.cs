using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using Wpf.Ui.Controls;

namespace HeliShare.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для Confirmation.xaml
    /// </summary>
    /// 

    public partial class FileAcceptWindow : FluentWindow
    {
        public ImageSource FileIcon { get; set; }
        public string FileName { get; }
        public FileAcceptWindow(string name)
        {
            InitializeComponent();
            FileName = name;
            FileIcon = HeliShare.Helpers.FileIconHelper.GetIconByFileName(name);
            DataContext = this;
        }
        void Yes(object s, RoutedEventArgs e) => DialogResult = true;
        void No(object s, RoutedEventArgs e) => DialogResult = false;
    }
}
