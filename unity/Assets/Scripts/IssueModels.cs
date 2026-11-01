using System;
using UnityEngine;

namespace Inspection
{
    [Serializable] public class PositionData
    {
        public float x, y, z;
        public PositionData(Vector3 value) { x = value.x; y = value.y; z = value.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }
    [Serializable] public class Issue
    {
        public string id, title, description, priority, status, createdAt, updatedAt;
        public PositionData position;
    }
    [Serializable] public class CreateIssue
    {
        public string title, description, priority;
        public PositionData position;
    }
    [Serializable] public class IssueResponse { public Issue data; }
    [Serializable] public class IssueListResponse { public Issue[] data; }
    [Serializable] public class ApiError { public string code, message, field; }
    [Serializable] public class ErrorResponse { public ApiError error; }
}
