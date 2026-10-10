using SharedKernel;

namespace Application.Legal;

public static class LegalErrors
{
    public static readonly Error TermsVersionMismatch = Error.Validation(
        "legal.terms_version_mismatch",
        "Debe aceptar la versión vigente de los Términos y Condiciones.");
}
