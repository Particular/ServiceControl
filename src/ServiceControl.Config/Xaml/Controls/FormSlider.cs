namespace ServiceControl.Config.Xaml.Controls
{
    using System;
    using System.ComponentModel;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;

    [DefaultEvent("ValueChanged")]
    [DefaultProperty("TimeSpan")]
    [TemplatePart(Name = SliderPartName, Type = typeof(Slider))]
    public class FormSlider : Slider
    {
        const string SliderPartName = "PART_Slider";

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var slider = GetTemplateChild(SliderPartName) as Slider;
            if (slider != null)
            {
                BindingOperations.SetBinding(slider, ValueProperty, new Binding("Value")
                {
                    Mode = BindingMode.TwoWay,
                    Source = this
                });
                BindingOperations.SetBinding(slider, MaximumProperty, new Binding("Maximum")
                {
                    Mode = BindingMode.TwoWay,
                    Source = this
                });
                BindingOperations.SetBinding(slider, MinimumProperty, new Binding("Minimum")
                {
                    Mode = BindingMode.TwoWay,
                    Source = this
                });
                BindingOperations.SetBinding(slider, SmallChangeProperty, new Binding("SmallChange")
                {
                    Mode = BindingMode.TwoWay,
                    Source = this
                });
                BindingOperations.SetBinding(slider, LargeChangeProperty, new Binding("LargeChange")
                {
                    Mode = BindingMode.TwoWay,
                    Source = this
                });
            }
        }

        static FormSlider()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormSlider), new FrameworkPropertyMetadata(typeof(FormSlider)));

            /*
            
            ### PETE - Help

            var originalMetadata = ValueProperty.GetMetadata(typeof(Slider));

            ValueProperty.OverrideMetadata(typeof(FormSlider),
                new FrameworkPropertyMetadata(
                    originalMetadata.DefaultValue,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                    originalMetadata.PropertyChangedCallback,
                    originalMetadata.CoerceValueCallback,
                    true,
                    UpdateSourceTrigger.PropertyChanged));
            */
        }

        public string Header
        {
            get { return (string) GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public string Summary
        {
            get { return (string)GetValue(SummaryProperty); }
            set { SetValue(SummaryProperty, value); }
        }

        public string Explaination
        {
            get { return (string)GetValue(ExplainationProperty); }
            set { SetValue(ExplainationProperty, value); }
        }

        public TimeSpanUnits Units
        {
            get { return (TimeSpanUnits) GetValue(UnitsProperty); }
            set { SetValue(UnitsProperty, value); }
        }

        public TimeSpan TimeSpan
        {
            get { return (TimeSpan) GetValue(TimeSpanProperty); }
            set { SetValue(TimeSpanProperty, value); }
        }

        public static readonly DependencyProperty TimeSpanProperty =
            DependencyProperty.Register("TimeSpan", typeof(TimeSpan), typeof(FormSlider), new PropertyMetadata { DefaultValue = TimeSpan.MinValue });

        public static readonly DependencyProperty ExplainationProperty =
            DependencyProperty.Register("Explaination", typeof(string), typeof(FormSlider), new PropertyMetadata(""));

        public static readonly DependencyProperty SummaryProperty =
            DependencyProperty.Register("Summary", typeof(string), typeof(FormSlider), new PropertyMetadata(""));

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(FormSlider), new PropertyMetadata(""));

        public static readonly DependencyProperty UnitsProperty =
            DependencyProperty.Register("Units", typeof(TimeSpanUnits), typeof(FormSlider), new PropertyMetadata { DefaultValue = TimeSpanUnits.Days});

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ValueProperty)
            {
                var period = (Units == TimeSpanUnits.Days) ? TimeSpan.FromDays(Math.Truncate(Value)) 
                                                           :  TimeSpan.FromHours(Math.Truncate(Value));
                SetValue(TimeSpanProperty, period);
                var s = new StringBuilder();

                if (period.TotalHours < 24)
                {
                    s.AppendFormat("{0} Hours", period.Hours);
                }
                else
                {
                    s.AppendFormat("{0} Day{1}", period.Days, (period.Days > 1) ? "s" : "");
                    if (period.Hours != 0)
                    {
                        s.AppendFormat(" {0} Hour{1}", period.Hours, (period.Hours > 1) ? "s" : ""); 
                    }
                }
                SetValue(SummaryProperty, s.ToString());
            }
            else if (e.Property == TimeSpanProperty)
            {
                if (Units == TimeSpanUnits.Days)
                {
                    SetValue(ValueProperty, TimeSpan.TotalDays);
                }
                else
                {
                    SetValue(ValueProperty, TimeSpan.TotalHours);
                }
            }
        }
    }

    public enum TimeSpanUnits
    {
        Hours,
        Days
    }
}
