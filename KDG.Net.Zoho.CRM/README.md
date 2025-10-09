# KDG .NET Zoho CRM Connector

A .NET library for integrating with Zoho CRM API, providing easy access to CRM data with support for incremental synchronization using the X-Modified-Since header.

## Features

- **Full CRUD Operations**: Create, read, update, and delete CRM records
- **Incremental Sync**: Support for X-Modified-Since header to fetch only modified records
- **Pagination Support**: Efficient handling of large datasets with built-in pagination
- **Search Functionality**: Advanced search with criteria-based filtering
- **Related Records**: Access to related record data
- **Retry Logic**: Built-in retry mechanism for robust API communication
- **Type Safety**: Generic methods for strong typing of your CRM models

## Quick Start

### Basic Usage

```csharp
// Initialize the connector
var config = new CRMConfig
{
    ApiVersion = "v6",
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    RefreshToken = "your-refresh-token",
    Scope = new[] { "ZohoCRM.modules.ALL" }
};

var connector = new CRMConnector(config, logger, serializer, serializerSettings, clock);

// Get all contacts
var contacts = await connector.GetRecords<Contact>("Contacts", new[] { "First_Name", "Last_Name", "Email" });

// Get contacts modified since yesterday
var modifiedContacts = await connector.GetRecords<Contact>(
    "Contacts",
    new[] { "First_Name", "Last_Name", "Email" },
    DateTime.Now.AddDays(-1)
);
```

## X-Modified-Since Header Support

The connector now supports the X-Modified-Since header for efficient incremental synchronization. This feature allows you to fetch only records that have been modified since a specific date and time.

### Supported Methods

All major retrieval methods now support an optional `DateTime? modifiedSince` parameter:

- `GetRecords<T>(module, fields, modifiedSince)`
- `GetRecordsPaginated<T>(module, fields, page, perPage, modifiedSince)`
- `GetRecord<T>(module, id, fields, modifiedSince)`
- `GetRelatedRecords<T>(module, id, relatedListApiName, fields, modifiedSince)`
- `GetRelatedRecordsPaginated<T>(module, id, relatedListApiName, fields, page, perPage, modifiedSince)`

### Search with X-Modified-Since

The `SearchParams` class now includes a `ModifiedSince` property:

```csharp
var searchParams = new SearchParams(
    "Contacts",
    new List<Criteria> { /* your criteria */ },
    DateTime.Now.AddHours(-1) // Only records modified in the last hour
);

var results = await connector.Search<Contact>(searchParams);
```

### Examples

#### Incremental Sync Pattern

```csharp
// Store last sync time
var lastSyncTime = GetLastSyncTime(); // Your implementation

// Get only modified records
var modifiedContacts = await connector.GetRecords<Contact>(
    "Contacts",
    new[] { "id", "First_Name", "Last_Name", "Email", "Modified_Time" },
    lastSyncTime
);

// Process modified records
ProcessContacts(modifiedContacts);

// Update last sync time
SaveLastSyncTime(DateTime.UtcNow);
```

#### Paginated Incremental Sync

```csharp
var lastSyncTime = DateTime.Now.AddDays(-7); // Last week
int page = 1;
const int pageSize = 200;
bool hasMore = true;

while (hasMore)
{
    var response = await connector.GetRecordsPaginated<Contact>(
        "Contacts",
        new[] { "id", "First_Name", "Last_Name", "Email" },
        page,
        pageSize,
        lastSyncTime
    );

    ProcessContacts(response.Results);

    hasMore = response.Results.Count == pageSize;
    page++;
}
```

### Date Format

The X-Modified-Since header uses ISO 8601 format in UTC:
- Format: `yyyy-MM-ddTHH:mm:ssZ`
- Example: `2024-01-15T10:30:00Z`

All DateTime values are automatically converted to UTC before being sent to the API.

## API Reference

### Core Methods

#### GetRecords
```csharp
// Get all records
Task<List<T>> GetRecords<T>(string module, IEnumerable<string> fields)

// Get records modified since specific date
Task<List<T>> GetRecords<T>(string module, IEnumerable<string> fields, DateTime? modifiedSince)
```

#### GetRecordsPaginated
```csharp
// Get paginated records
Task<PaginatedApiResponse<T>> GetRecordsPaginated<T>(string module, IEnumerable<string> fields, int page, int perPage)

// Get paginated records with modification filter
Task<PaginatedApiResponse<T>> GetRecordsPaginated<T>(string module, IEnumerable<string> fields, int page, int perPage, DateTime? modifiedSince)
```

#### Search
```csharp
// Search with criteria and optional modification filter
Task<ApiResponse<T>> Search<T>(SearchParams search)
```

### Configuration

The `CRMConfig` class requires the following properties:
- `ApiVersion`: Zoho CRM API version (e.g., "v6")
- `ClientId`: OAuth client ID
- `ClientSecret`: OAuth client secret
- `RefreshToken`: OAuth refresh token
- `Scope`: API scopes (e.g., ["ZohoCRM.modules.ALL"])

### Error Handling

The connector includes built-in retry logic and throws `TooManyRetries` exception when the maximum retry attempts are exceeded.

## Breaking Changes

This update maintains full backward compatibility. All existing method signatures continue to work without modification.

## Support

For support, please open an issue on our [GitHub Issues page](https://github.com/KDG-Development/KDG-Net-Zoho-CRM/issues) and provide your questions or feedback. We strive to address all inquiries promptly.

## Contributing

To contribute to this project, please follow these steps:

1. Fork the repository to your own GitHub account.
2. Make your changes and commit them to your fork.
3. Submit a pull request to the original repository with a clear description of what your changes do and why they should be included.
