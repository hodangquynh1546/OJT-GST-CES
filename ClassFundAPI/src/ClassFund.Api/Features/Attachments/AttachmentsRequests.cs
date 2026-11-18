using FluentValidation;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Attachments;

/// <summary>Multipart <c>file</c> + <c>kind</c> (Receipt / PaymentProof / Document).</summary>
public sealed class UploadAttachmentRequest
{
    public IFormFile? File { get; init; }

    public AttachmentKind? Kind { get; init; }
}

/// <summary>Multipart <c>file</c> cho ảnh đại diện.</summary>
public sealed class UploadAvatarRequest
{
    public IFormFile? File { get; init; }
}

public static class UploadLimits
{
    /// <summary>Lớn nhất trong các loại (tài liệu 10 MB) + phần đầu multipart; giới hạn từng loại do FileRule kiểm.</summary>
    public const long MaxRequestBytes = (10 * 1024 * 1024) + (64 * 1024);

    public const long MaxAvatarRequestBytes = (2 * 1024 * 1024) + (64 * 1024);
}

public sealed class UploadAttachmentRequestValidator : AbstractValidator<UploadAttachmentRequest>
{
    public UploadAttachmentRequestValidator()
    {
        RuleFor(request => request.File).NotNull().WithMessage("Vui lòng chọn file.");
        RuleFor(request => request.Kind)
            .NotNull().WithMessage("Thiếu loại file.")
            .Must(kind => kind is null or AttachmentKind.Receipt or AttachmentKind.PaymentProof or AttachmentKind.Document)
            .WithMessage("Loại file phải là Receipt, PaymentProof hoặc Document.");
    }
}

public sealed class UploadAvatarRequestValidator : AbstractValidator<UploadAvatarRequest>
{
    public UploadAvatarRequestValidator() =>
        RuleFor(request => request.File).NotNull().WithMessage("Vui lòng chọn ảnh.");
}
