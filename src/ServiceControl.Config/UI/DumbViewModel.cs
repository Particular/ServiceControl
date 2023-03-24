using FluentValidation;
using ServiceControl.Config.Framework.Rx;
using Validar;

namespace ServiceControl.Config.UI
{
   [InjectValidation]
   public class DumbViewModel: RxScreen
   {
        public string Prop { get; set; }
   }

    public class DumbViewModelValidator: AbstractValidator<DumbViewModel> {
        public DumbViewModelValidator()
        {
            RuleFor(vm => vm.Prop).NotEmpty().WithMessage("Should not be empty");
        }
    }
  
}
