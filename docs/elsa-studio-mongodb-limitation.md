# Elsa Studio MongoDB Integration Limitation

**Status:** ❌ **FAILED** - MongoDB integration not supported in official Elsa Studio Docker image  
**Date:** April 7, 2026  
**Elsa Version:** 3.6.0  
**Docker Image:** `elsaworkflows/elsa-server-and-studio-v3:latest`

---

## Problem Summary

The official Elsa Studio Docker image (`elsaworkflows/elsa-server-and-studio-v3`) **does not support MongoDB** despite configuration attempts. The Studio component always defaults to SQLite regardless of database provider environment variables.

## Attempted Configuration

### Environment Variables Tested
```bash
DATABASEPROVIDER=MongoDb
CONNECTIONSTRINGS__MONGODB=mongodb://admin:admin@mongodb:27017/elsa?authSource=admin
ConnectionStrings__Default=mongodb://admin:admin@host.docker.internal:27017/elsa?authSource=admin
```

### Docker Commands Attempted
```bash
# Attempt 1: Direct environment variables
docker run -d --name elsa-studio \
  -p 6001:8080 \
  -e DATABASEPROVIDER=MongoDb \
  -e CONNECTIONSTRINGS__MONGODB="mongodb://admin:admin@mongodb:27017/elsa?authSource=admin" \
  elsaworkflows/elsa-server-and-studio-v3:latest

# Attempt 2: Legacy connection string format
docker run -d --name elsa-studio-server \
  -e ConnectionStrings__Default="mongodb://admin:admin@host.docker.internal:27017/elsa?authSource=admin" \
  elsaworkflows/elsa-server-and-studio-v3
```

## Observed Behavior

### What We Expected
- Elsa Studio connects to MongoDB
- Workflows created in Studio UI stored in MongoDB
- Unified storage between Studio and Domain API

### What Actually Happened
- ✅ Environment variables correctly set in container (`docker exec elsa-studio env`)
- ❌ Studio ignores MongoDB configuration entirely  
- ❌ Studio creates SQLite files: `/app/elsa.sqlite.db`, `/app/elsa.sqlite.db-shm`, `/app/elsa.sqlite.db-wal`
- ❌ Workflows created in Studio UI stored in SQLite only
- ✅ Domain API correctly connects to MongoDB when configured

### Verification Commands
```bash
# Environment variables are present
docker exec elsa-studio env | grep DATABASE
# Output: DATABASEPROVIDER=MongoDb

# But SQLite files are created anyway
docker exec elsa-studio find /app -name "*.sqlite*"
# Output: /app/elsa.sqlite.db, /app/elsa.sqlite.db-shm, /app/elsa.sqlite.db-wal
```

## Root Cause Analysis

### Docker Image Investigation
The `elsaworkflows/elsa-server-and-studio-v3` image appears to:
1. **Hardcode SQLite** as the database provider for Studio UI components
2. **Ignore environment variables** related to database configuration  
3. **Not include MongoDB drivers** or proper configuration mapping

### Evidence
- Multiple Docker image versions tested (`latest`, tagged versions)
- Various environment variable formats attempted
- Container networking verified working (MongoDB accessible at `mongodb:27017`)
- Same MongoDB configuration works perfectly in custom .NET applications

## Current Workaround

### Dual Storage Architecture
Since MongoDB integration failed, we're using a **dual storage model**:

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  Elsa Studio    │    │   Domain API     │    │    MongoDB      │
│  (SQLite)       │    │   (MongoDB)      │    │                 │
│                 │    │                 │    │                 │
│ • UI Workflows  │    │ • Code Workflows │    │ • Domain Data   │
│ • Design/Test   │    │ • Production     │    │ • Workflows     │
│ • Prototyping   │    │ • Business Logic │    │ • Persistence   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Usage Patterns
- **Studio (SQLite)**: Visual workflow design, testing, prototyping
- **Domain API (MongoDB)**: Production workflow execution, business data persistence

## Alternative Solutions Investigated

### 1. Custom Elsa Studio Build
**Status:** Not attempted - would require significant effort  
**Approach:** Build custom Docker image with proper MongoDB support  
**Risk:** Maintenance burden, version compatibility issues

### 2. Embedded Elsa Studio  
**Status:** Previously attempted and failed  
**Issue:** Complex integration with existing domain API architecture  
**Reference:** See ADR-007 for embedded approach decision

### 3. Separate Elsa Server + Studio
**Status:** Not tested  
**Approach:** Use separate `elsaworkflows/elsa-server` image with MongoDB support  
**Unknown:** Whether Studio can connect to external MongoDB-backed Elsa Server

## Recommendations

### Short Term ✅
**Accept dual storage architecture:**
- Use Studio for workflow design and testing (SQLite is acceptable for this)
- Use Domain API for production workflows (MongoDB working correctly)
- Document clear separation of concerns

### Long Term 🔍
**Monitor Elsa ecosystem for:**
- Official MongoDB support in Studio Docker image
- Community solutions or alternatives
- Elsa Studio v4+ roadmap for database provider flexibility

## Impact Assessment

### ✅ What Still Works
- Complete workflow execution in Domain API
- MongoDB persistence for business entities
- Code-first workflow definitions
- Production workflow orchestration
- Visual workflow design in Studio (with separate storage)

### ❌ What Doesn't Work  
- Unified storage between Studio and API
- MongoDB-backed visual workflow persistence
- Single source of truth for all workflows

### 📊 Business Impact
**Low** - Dual storage doesn't prevent production usage, just creates some operational complexity in development/testing workflows.

---

## Conclusion

The Elsa Studio MongoDB integration is **not currently supported** in official Docker images. This appears to be a limitation of the containerized distribution rather than the framework itself. 

The dual storage workaround provides full functionality while we monitor the Elsa ecosystem for future MongoDB support in Studio components.