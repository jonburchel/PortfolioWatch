using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace PortfolioWatch.Views
{
    public partial class InputWindow : Window, INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private string _inputText = string.Empty;
        private bool _isCheckBoxChecked;
        private readonly Func<string, string?>? _validator;
        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(InputWindow), new PropertyMetadata(string.Empty));

        public string CheckBoxText
        {
            get { return (string)GetValue(CheckBoxTextProperty); }
            set { SetValue(CheckBoxTextProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxTextProperty =
            DependencyProperty.Register("CheckBoxText", typeof(string), typeof(InputWindow), new PropertyMetadata(string.Empty));

        public string RadioOption1
        {
            get { return (string)GetValue(RadioOption1Property); }
            set { SetValue(RadioOption1Property, value); }
        }

        public static readonly DependencyProperty RadioOption1Property =
            DependencyProperty.Register("RadioOption1", typeof(string), typeof(InputWindow), new PropertyMetadata(string.Empty));

        public string RadioOption2
        {
            get { return (string)GetValue(RadioOption2Property); }
            set { SetValue(RadioOption2Property, value); }
        }

        public static readonly DependencyProperty RadioOption2Property =
            DependencyProperty.Register("RadioOption2", typeof(string), typeof(InputWindow), new PropertyMetadata(string.Empty));

        public bool IsCheckBoxVisible => !string.IsNullOrEmpty(CheckBoxText) && string.IsNullOrEmpty(RadioOption1);
        public bool IsRadioOptionsVisible => !string.IsNullOrEmpty(RadioOption1);

        public bool IsCheckBoxChecked
        {
            get => _isCheckBoxChecked;
            set
            {
                if (_isCheckBoxChecked != value)
                {
                    _isCheckBoxChecked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPrimaryOptionChecked));
                }
            }
        }

        public bool IsPrimaryOptionChecked
        {
            get => !_isCheckBoxChecked;
            set
            {
                if (value)
                {
                    IsCheckBoxChecked = false;
                }
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                if (_inputText != value)
                {
                    _inputText = value;
                    OnPropertyChanged();
                    ValidateInput();
                }
            }
        }

        public bool HasErrors => _errors.Any();
        public bool IsInputValid => !HasErrors;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public InputWindow(string message, string title, string defaultText = "", Func<string, string?>? validator = null, string checkBoxText = "", string radioOption1 = "", string radioOption2 = "")
        {
            InitializeComponent();
            DataContext = this;
            Message = message;
            Title = title;
            _validator = validator;
            InputText = defaultText;
            CheckBoxText = checkBoxText;
            RadioOption1 = radioOption1;
            RadioOption2 = radioOption2;
            
            // Initial validation
            ValidateInput();
            
            InputBox.Focus();
            InputBox.SelectAll();
        }

        private void ValidateInput()
        {
            _errors.Clear();
            if (_validator != null)
            {
                var error = _validator(InputText);
                if (!string.IsNullOrEmpty(error))
                {
                    _errors[nameof(InputText)] = new List<string> { error };
                }
            }
            
            OnErrorsChanged(nameof(InputText));
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(IsInputValid));
        }

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || !_errors.ContainsKey(propertyName))
                return Enumerable.Empty<string>();
            return _errors[propertyName];
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsInputValid)
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (IsInputValid)
                {
                    DialogResult = true;
                    Close();
                }
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}
