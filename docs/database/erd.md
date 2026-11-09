# ERD

> Phiên bản 2.0 · 2026-11-05 – thiết kế hiện hành (migration `InitialClassFund` + `AddDatabaseProtections`). Trạng thái triển khai: **Implemented trong Foundation** (schema dùng chung cho mọi module). Thiết kế `FundMember` v1.1 / v1.2 không dùng (Git history).

```mermaid
erDiagram
    User ||--o{ Fund : "phụ trách – GVCN (OwnerUserId)"
    Fund ||--o{ Student : "có học sinh"
    User |o--o| Student : "tài khoản học sinh (UserId, UNIQUE)"
    Student ||--o{ ParentLink : "liên kết phụ huynh (≤1 đang hiệu lực)"
    User ||--o{ ParentLink : "là phụ huynh (ParentUserId)"
    Fund ||--o{ TreasurerAssignment : "phân công thủ quỹ"
    Student ||--o{ TreasurerAssignment : "được phân công (cùng quỹ, ≤1 đang hiệu lực)"
    Fund ||--o{ Category : "có"
    Fund ||--o{ FundTransaction : "có"
    Category ||--o{ FundTransaction : "phân loại (cùng quỹ, cùng loại)"
    User ||--o{ AuditLog : "thực hiện"

    User { uuid Id PK; varchar Kind; varchar Email; varchar NormalizedEmail UK; varchar Username UK; varchar FullName; varchar PasswordHash; varchar Status; bool MustChangePassword; varchar SecurityStamp }
    Fund { uuid Id PK; uuid OwnerUserId FK; varchar Name }
    Student { uuid Id PK; uuid FundId FK; varchar FullName; uuid UserId FK_UK; uuid CreatedBy FK }
    ParentLink { uuid Id PK; uuid StudentId FK; uuid ParentUserId FK; bool IsActive; uuid CreatedBy FK; uuid RemovedBy FK }
    TreasurerAssignment { uuid Id PK; uuid FundId FK; uuid StudentId FK; uuid AssignedBy FK; timestamptz AssignedAt; uuid RevokedBy FK; timestamptz RevokedAt }
    Category { uuid Id PK; uuid FundId FK; varchar Type; varchar Name; bool IsHidden }
    FundTransaction { uuid Id PK; uuid FundId FK; varchar Type; bigint Amount; date OccurredOn; uuid CategoryId FK; varchar Description; varchar Note; bool IsPublic; bool IsCancelled; varchar CancelReason; uuid CreatedBy FK; uuid UpdatedBy FK; uuid CancelledBy FK; uuid IdempotencyKey }
    AuditLog { bigint Id PK; timestamptz OccurredAt; uuid ActorUserId FK; varchar Action; jsonb OldValues; jsonb NewValues }
```

Vai trò trong quỹ **không** có bảng: GVCN = `Fund.OwnerUserId`; Thủ quỹ = `Student.UserId` + `TreasurerAssignment` đang hiệu lực; Học sinh = `Student.UserId`; Phụ huynh = `ParentLink` đang hiệu lực.
