-- Create DeltaCursors table for Delta query implementation
CREATE TABLE IF NOT EXISTS DeltaCursors (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ScopeId TEXT NOT NULL,
    CustodianEmail TEXT NOT NULL,
    DeltaType TEXT NOT NULL,
    DeltaToken TEXT NOT NULL,
    CollectionJobId INTEGER,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    CreatedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastDeltaTime DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DeltaQueryCount INTEGER NOT NULL DEFAULT 0,
    LastDeltaItemCount INTEGER NOT NULL DEFAULT 0,
    LastDeltaSizeBytes INTEGER NOT NULL DEFAULT 0,
    BaselineCompletedAt DATETIME,
    ErrorMessage TEXT,
    Metadata TEXT
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS IX_DeltaCursors_ScopeId ON DeltaCursors(ScopeId);
CREATE INDEX IF NOT EXISTS IX_DeltaCursors_CustodianEmail ON DeltaCursors(CustodianEmail);
CREATE INDEX IF NOT EXISTS IX_DeltaCursors_DeltaType ON DeltaCursors(DeltaType);
CREATE INDEX IF NOT EXISTS IX_DeltaCursors_IsActive ON DeltaCursors(IsActive);
CREATE INDEX IF NOT EXISTS IX_DeltaCursors_LastDeltaTime ON DeltaCursors(LastDeltaTime);

-- Insert sample delta cursors for testing
INSERT INTO DeltaCursors (ScopeId, CustodianEmail, DeltaType, DeltaToken, CollectionJobId) VALUES
('mail-user1@company.com', 'user1@company.com', 'Mail', 'initial-mail-token-1', 1),
('onedrive-user1@company.com', 'user1@company.com', 'OneDrive', 'initial-onedrive-token-1', 1),
('mail-user2@company.com', 'user2@company.com', 'Mail', 'initial-mail-token-2', 2),
('onedrive-user2@company.com', 'user2@company.com', 'OneDrive', 'initial-onedrive-token-2', 2);