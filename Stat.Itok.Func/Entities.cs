using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stat.Itok.Func
{
    public class NinAuthContextValidator : AbstractValidator<NinAuthContext>
    {
        public NinAuthContextValidator()
        {
            RuleFor(x => x).NotNull();
            RuleFor(x => x.UserInfo).NotNull();
            RuleFor(x => x.UserInfo.Id).NotNull();
            RuleFor(x => x.SessionToken).NotEmpty(); ;
        }
    }
}
