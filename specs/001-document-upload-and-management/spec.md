# Feature Specification: Document Upload and Management

**Feature Branch**: `001-document-upload-and-management`  
**Created**: 2026-09-18  
**Status**: Clarified  
**Input**: Stakeholder requirements: `StakeholderDocs/document-upload-and-management-feature.md`

## Clarifications

### Session 2026-09-18

- Q: No virus scanning engine is available in the offline training environment. What
  satisfies the "scan for viruses and malware" requirement? → A: Validation stands in for
  scanning: the extension must be on the allow list, the declared content type must match
  the extension, the first bytes of the file must match the signature expected for that
  type, and the size limit is enforced before the file is written. The scan itself is
  represented by a content-validation step in the upload pipeline that a real scanning
  service can be plugged into for production, so no page or service changes are needed
  later.
- Q: What happens to documents a user uploaded to a project after that user is removed from
  the project? → A: The documents stay associated with the project and remain visible to
  the project's members and manager. The original uploader keeps owner rights (edit,
  replace, delete) but loses the project-membership route to the project's other documents.
- Q: What happens to documents associated with a project when the project is deleted? →
  A: The documents are unlinked from the project rather than deleted. They remain in the
  uploader's document list as personal documents, and access falls back to uploader,
  recipients of shares, and administrators.
- Q: What happens to recipients when the owner deletes a document that has been shared? →
  A: Deletion is permanent and removes the share records; recipients lose access
  immediately and receive an in-app notification that the document is no longer available.
  There is no trash or recovery, consistent with the out-of-scope list.
- Q: How are file names with special characters, spaces, or path segments handled? →
  A: The original file name is kept only as display metadata and is sanitized before it is
  returned in a download header. The stored file always uses a server-generated GUID name,
  so no user-supplied text ever forms part of a path.

### Session 2026-09-18 (round 2)

- Q: What should happen when storage is full or the upload directory is not writable? →
  A: The upload fails with a message telling the user to retry later, any partially written
  file is removed, no metadata record is created, and the failure is logged for
  administrators.
- Q: Does "Shared with Me" include documents a user can see through project membership? →
  A: No. "Shared with Me" lists only documents explicitly shared with the user. Project
  documents are reached from the project view and from search.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload a work document (Priority: P1)

An employee who has just finished a status report opens the dashboard, goes to Documents,
picks the file from their computer, gives it a title and a category, optionally links it to
a project, and uploads it. The upload shows progress, confirms success, and the document
appears at the top of their document list with its size, category, and upload date.

**Why this priority**: Nothing else in the feature has value until documents can get into
the system. This single journey replaces the current practice of keeping work files on
local drives and in email attachments.

**Independent Test**: Log in as an employee, upload a PDF smaller than 25 MB with a title
and category, and confirm the document is listed with correct metadata and is retrievable
from disk. Delivers value on its own: a centralized, attributed store of work documents.

**Acceptance Scenarios**:

1. **Given** an authenticated employee on the Documents page, **When** they select a 2 MB
   PDF, enter the title "Q3 Status Report", choose the category "Reports", and submit,
   **Then** the system stores the file, records the metadata with the employee as uploader
   and the current UTC time, and shows a success message.
2. **Given** the upload from scenario 1 has completed, **When** the employee views their
   document list, **Then** "Q3 Status Report" appears with its category, file size, file
   type, and upload date.
3. **Given** an employee selects a file of 30 MB, **When** they submit the upload,
   **Then** the system rejects it with a message stating the 25 MB limit and stores
   neither the file nor a metadata record.
4. **Given** an employee selects a file with an unsupported extension such as `.exe`,
   **When** they submit the upload, **Then** the system rejects it with a message listing
   the supported types.
5. **Given** an employee chooses a project they are not a member of, **When** they submit
   the upload, **Then** the system rejects the upload with an authorization message.

---

### User Story 2 - Find and open documents (Priority: P1)

An employee needs a file they uploaded last month. They open Documents, filter by category
or project, sort by upload date, or type part of the title into search, and download or
preview the document from the result list.

**Why this priority**: A centralized store that cannot be searched is no better than a
shared drive. Retrieval is what shortens "time to locate a document", the main business
metric for the feature.

**Independent Test**: With several documents uploaded across categories and projects,
filter, sort, and search the list, then download one file and confirm the bytes match what
was uploaded.

**Acceptance Scenarios**:

1. **Given** an employee with documents in several categories, **When** they filter by
   "Reports", **Then** only documents in that category are listed.
2. **Given** an employee with 20 documents, **When** they sort by upload date descending,
   **Then** the most recently uploaded document is first.
3. **Given** an employee searching for "budget", **When** the term appears in a document
   title, description, or tags, **Then** the matching documents are returned within
   2 seconds, and documents the employee may not access are never returned.
4. **Given** an employee viewing their list, **When** they select Download on a document
   they own, **Then** the original file is returned with its original file name and
   content type.
5. **Given** an employee who knows the identifier of a document owned by someone else and
   not shared with them, **When** they request that download directly, **Then** the system
   refuses the request instead of returning the file.

---

### User Story 3 - Share documents with colleagues (Priority: P2)

A team lead shares an onboarding checklist with two team members. Each recipient receives
an in-app notification and finds the document under "Shared with Me", from where they can
open and download it but not delete it.

**Why this priority**: Controlled sharing is the stated replacement for emailing
attachments, but it depends on upload and retrieval already working.

**Independent Test**: Share one document with another user, log in as that user, and
confirm the notification and the "Shared with Me" entry, and that download succeeds while
delete does not.

**Acceptance Scenarios**:

1. **Given** a document owner, **When** they share the document with a colleague,
   **Then** the colleague receives an in-app notification naming the document and the
   sharer.
2. **Given** a colleague with a document shared with them, **When** they open "Shared with
   Me", **Then** the document is listed with its owner and shared date and can be
   downloaded.
3. **Given** a colleague with a shared document, **When** they attempt to delete or edit
   its metadata, **Then** the system refuses the action.
4. **Given** a document already shared with a colleague, **When** the owner shares it with
   the same colleague again, **Then** no duplicate entry or duplicate notification is
   created.

---

### User Story 4 - Organize project documents (Priority: P2)

A project manager opens a project and sees every document associated with it, uploaded by
any team member. Team members can view and download those documents; the project manager
can additionally remove documents that no longer belong.

**Why this priority**: Project association is what gives documents their context, and it is
the reason the stakeholders asked for the feature to live inside the dashboard rather than
in a separate tool.

**Independent Test**: Upload documents to a project from two different member accounts,
then confirm both are visible to every project member and to the project manager, and that
the project manager can delete either one.

**Acceptance Scenarios**:

1. **Given** a project with documents uploaded by two members, **When** any member of that
   project views the project's documents, **Then** all documents associated with the
   project are listed with their uploader.
2. **Given** a project manager viewing their project's documents, **When** they delete a
   document uploaded by a member, **Then** the document and its stored file are removed.
3. **Given** an employee who is not a member of a project, **When** they view that
   project's documents, **Then** none are listed and direct access is refused.
4. **Given** a document is uploaded to a project, **When** the upload completes, **Then**
   the other project members receive an in-app notification about the new document.

---

### User Story 5 - Maintain and remove documents (Priority: P3)

An employee corrects the title and category of a document they uploaded, replaces the file
with a newer version, and later deletes a document they no longer need after confirming the
deletion.

**Why this priority**: Correction and cleanup keep the store trustworthy, but the feature
delivers value before they exist.

**Independent Test**: Edit metadata on an owned document, replace its file, then delete it
and confirm both the record and the stored file are gone.

**Acceptance Scenarios**:

1. **Given** an owner of a document, **When** they change the title, description, category,
   or tags, **Then** the changes are saved and shown in the list.
2. **Given** an owner of a document, **When** they replace the file with a valid new file,
   **Then** the new file is stored, the previous file is removed from storage, and the
   size, type, and file name metadata are updated.
3. **Given** an owner of a document, **When** they confirm deletion, **Then** the metadata
   record, the stored file, and any share entries for that document are removed.
4. **Given** a user who is neither the owner nor the manager of the document's project,
   **When** they attempt deletion, **Then** the system refuses the action.

---

### Edge Cases

- What happens when the file is saved to storage but the database write fails? The stored
  file must not be left behind without a record.
- What happens when a user uploads two files with the same name? Both must be stored
  without overwriting each other.
- What happens when a file name contains characters that are not valid in a path, or an
  attempt at directory traversal such as `../../secrets.txt`?
- What happens when a user is removed from a project after uploading documents to it?
- What happens when a project that has documents is deleted?
- What happens when the owner deletes a document that has been shared with others?
- What happens when the storage directory is not writable or the disk is full?
- What happens when a user selects no file, or leaves the title or category empty?
- What happens when a document's stored file is missing at download time?

## Requirements *(mandatory)*

### Functional Requirements

**Upload**

- **FR-001**: Users MUST be able to select a file from their computer and upload it from
  the Documents page.
- **FR-002**: System MUST accept only PDF, Word, Excel, PowerPoint, plain text, JPEG, and
  PNG files, validated by file extension against an allow list.
- **FR-003**: System MUST reject any file larger than 25 MB and report the limit to the
  user.
- **FR-004**: System MUST require a title and a category for every upload, where the
  category is one of: Project Documents, Team Resources, Personal Files, Reports,
  Presentations, Other.
- **FR-005**: Users MUST be able to provide an optional description, optional tags, and an
  optional associated project when uploading.
- **FR-006**: System MUST record the uploader, upload date and time in UTC, original file
  name, file size, and content type for every document, and MUST support content types up
  to 255 characters.
- **FR-007**: System MUST show upload progress and a success or failure message for every
  upload attempt.
- **FR-008**: System MUST allow a document to be associated with a project only when the
  uploading user manages or is a member of that project.
- **FR-009**: System MUST generate a storage path before writing the file, write the file
  to storage, and only then create the metadata record; if the metadata record cannot be
  created, the written file MUST be removed.
- **FR-010**: System MUST store uploaded files outside any web-accessible directory, under
  a server-generated name of the form `{userId}/{projectId or "personal"}/{guid}.{ext}`,
  and MUST never use a user-supplied file name as part of the path.
- **FR-011**: System MUST validate uploaded content before the file is made available for
  download, by checking the extension against the allow list, confirming that the declared
  content type matches the extension, and confirming that the leading bytes of the file
  match the signature expected for that type; content that fails any check MUST be
  rejected and MUST NOT be stored.
- **FR-011a**: The content validation step MUST be replaceable, so that a virus scanning
  service can be introduced for production without changes to pages, services, or schema.
- **FR-011b**: System MUST keep the original file name as display metadata only, MUST
  sanitize it before returning it in a download, and MUST NOT use it to form a storage
  path.
- **FR-011c**: When a file cannot be written to storage, System MUST remove any partial
  file, MUST NOT create a metadata record, MUST report a retry message to the user, and
  MUST log the failure.

**Browse, search, and access**

- **FR-012**: Users MUST be able to view a list of the documents they uploaded, showing
  title, category, upload date, file size, and associated project.
- **FR-013**: Users MUST be able to sort their documents by title, upload date, category,
  or file size.
- **FR-014**: Users MUST be able to filter their documents by category, associated project,
  and upload date range.
- **FR-015**: Users MUST be able to search documents by title, description, tags, uploader
  name, and associated project name, and results MUST contain only documents the user is
  permitted to access.
- **FR-016**: Users MUST be able to download any document they are permitted to access, and
  the download MUST return the original file name and content type.
- **FR-017**: System MUST allow PDF and image documents to be previewed in the browser
  without downloading.
- **FR-018**: System MUST grant access to a document only to its uploader, the members and
  manager of its associated project, users it has been shared with, and administrators, and
  MUST verify this on every retrieval including direct requests by identifier.
- **FR-019**: Project members MUST be able to see all documents associated with their
  project from the project view.

**Sharing**

- **FR-020**: Document owners MUST be able to share a document with one or more other
  users.
- **FR-021**: Recipients MUST receive an in-app notification when a document is shared with
  them, and MUST see the document under "Shared with Me", which lists only documents shared
  explicitly and not documents reachable through project membership.
- **FR-022**: System MUST NOT create duplicate share records or repeat notifications when
  the same document is shared with the same user more than once.
- **FR-023**: Recipients of a shared document MUST be able to view and download it, and
  MUST NOT be able to edit its metadata or delete it.

**Maintenance**

- **FR-024**: Uploaders MUST be able to edit the title, description, category, and tags of
  their own documents.
- **FR-025**: Uploaders MUST be able to replace the file of their own documents with a new
  file that passes the same validation, and the superseded file MUST be removed from
  storage.
- **FR-026**: Uploaders MUST be able to delete their own documents, and project managers
  MUST be able to delete any document associated with their projects; deletion MUST require
  confirmation and MUST permanently remove the metadata record, the stored file, and any
  share records.
- **FR-027**: When a user is removed from a project, System MUST keep the documents they
  uploaded to that project associated with the project and visible to its members and
  manager, while the uploader retains the right to edit, replace, and delete those
  documents.
- **FR-028**: When a project is deleted, System MUST unlink its documents from the project
  rather than delete them; the documents remain in their uploader's list with no associated
  project.
- **FR-028a**: When a shared document is deleted, System MUST remove its share records and
  MUST notify the recipients that the document is no longer available.

**Integration**

- **FR-029**: System MUST show the five most recently uploaded documents of the current
  user in a "Recent Documents" widget on the dashboard home page.
- **FR-030**: System MUST show the current user's document count among the dashboard
  summary cards.
- **FR-031**: System MUST notify the other members of a project when a document is added to
  that project.
- **FR-032**: Users MUST be able to see documents related to a task and upload a document
  from a task, and such a document MUST be associated with the task's project.

**Audit**

- **FR-033**: System MUST record document uploads, downloads, deletions, and shares with
  the acting user, the document, and a UTC timestamp.
- **FR-034**: Administrators MUST be able to access all documents for audit and compliance
  purposes and to review the recorded activity.

### Non-Functional Requirements

- **NFR-001**: An upload of a file up to 25 MB MUST complete within 30 seconds on a typical
  office network.
- **NFR-002**: A document list MUST render within 2 seconds for up to 500 documents.
- **NFR-003**: Search MUST return results within 2 seconds.
- **NFR-004**: A document preview MUST load within 3 seconds.
- **NFR-005**: The feature MUST work with no internet connection and no cloud service.
- **NFR-006**: Storage MUST be reachable only through an interface with upload, download,
  delete, and URL operations, so that a cloud implementation can replace the local one
  without changes to pages, services, or database schema.
- **NFR-007**: Uploading a document MUST take no more than three interactions from the
  Documents page.

### Key Entities

- **Document**: A stored file and its metadata. Integer identifier; title, optional
  description, category held as text, optional tags, original file name, storage path,
  content type (up to 255 characters), size in bytes, upload timestamp, uploader, optional
  associated project. Belongs to exactly one uploader and at most one project.
- **DocumentShare**: A grant of access to a document for one user. References the document,
  the recipient, the user who shared it, and the time of sharing. Unique per document and
  recipient.
- **DocumentActivity**: A record of an action taken on a document — upload, download,
  update, delete, or share — with the acting user and a UTC timestamp.
- **User** *(existing)*: Uploader, recipient of shares, and subject of authorization checks.
- **Project** *(existing)*: Optional context for a document; its membership determines who
  may see project documents.
- **Notification** *(existing)*: Carries share and project-document announcements to users.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An employee can upload a document, from opening the Documents page to seeing
  it listed, in under 60 seconds and no more than three interactions.
- **SC-002**: An employee can locate a previously uploaded document in under 30 seconds
  using search or filters.
- **SC-003**: 100% of attempts to retrieve a document by identifier without permission are
  refused.
- **SC-004**: 100% of uploads that exceed 25 MB or use an unsupported file type are
  rejected with an explanatory message and leave no stored file and no metadata record.
- **SC-005**: Document lists and search results return within 2 seconds at 500 documents.
- **SC-006**: Every upload, download, share, and deletion appears in the activity record
  with its acting user and timestamp.
- **SC-007**: Within three months of launch, 70% of active dashboard users have uploaded at
  least one document and 90% of uploaded documents carry a category other than "Other".

## Assumptions

- The existing mock cookie authentication and the four existing roles are the authorization
  basis; no new identity system is introduced.
- Local disk storage is acceptable for the training environment, with cloud object storage
  planned for production.
- Most documents are under 10 MB.
- The development database is recreated from the model, so schema changes do not need a
  migration path for existing data.

## Out of Scope

- Collaborative editing, version history, and rollback.
- Approval workflows and document routing.
- Integration with SharePoint, OneDrive, or other external systems.
- Mobile applications.
- Document templates and document generation.
- Storage quotas.
- Soft delete or a recoverable trash.
