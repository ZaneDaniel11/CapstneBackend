using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using Request.Models;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RequestItemsApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";


        [HttpPost("InsertRequest")]
        public async Task<IActionResult> InsertRequestItem([FromBody] RequestItem requestItem)
        {
            string insertQuery = @"
        INSERT INTO RequestItems_tb (RequestedItem, Description, RequestedBy, SuggestedDealer, Purpose, EstimatedCost, RequestedDate, Status, Priority, Admin1Approval, Admin2Approval, Admin3Approval, BorrowerId, Email)
        VALUES (@RequestedItem, @Description, @RequestedBy, @SuggestedDealer, @Purpose, @EstimatedCost, @RequestedDate, @Status, @Priority, @Admin1Approval, @Admin2Approval, @Admin3Approval, @BorrowerId, @Email);
    ";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.ExecuteAsync(insertQuery, new
                {
                    requestItem.RequestedItem,
                    requestItem.Description, // Description field
                    requestItem.RequestedBy,
                    requestItem.SuggestedDealer,
                    requestItem.Purpose,
                    requestItem.EstimatedCost,
                    requestItem.RequestedDate,
                    Status = "Pending", // Default value for Status
                    requestItem.Priority,
                    Admin1Approval = "Pending",
                    Admin2Approval = "Pending",
                    Admin3Approval = "Pending",
                    requestItem.BorrowerId,
                    requestItem.Email // Include Email here
                });

                return Ok(result);
            }
        }

        // API for getting all request items
        [HttpGet("GetAllRequests")]
        public async Task<IActionResult> GetAllRequests()
        {
            string selectQuery = @"
                SELECT *
                FROM RequestItems_tb;
            ";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var requestItems = await connection.QueryAsync<RequestItem>(selectQuery);
                return Ok(requestItems);
            }
        }

        // API for updating a request's details
        [HttpPut("UpdateRequest/{requestId}")]
        public async Task<IActionResult> UpdateRequest(int requestId, [FromBody] RequestItem updatedRequest)
        {
            string updateQuery = @"
                UPDATE RequestItems_tb 
                SET RequestedItem = @RequestedItem,
                    SuggestedDealer = @SuggestedDealer,
                    Purpose = @Purpose,
                    EstimatedCost = @EstimatedCost,
                    RequestedDate = @RequestedDate,
                    Status = @Status,
                    Priority = @Priority
                WHERE RequestID = @RequestId;
            ";

            string selectQuery = @"
                SELECT * FROM RequestItems_tb WHERE RequestID = @RequestId;
            ";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                await connection.ExecuteAsync(updateQuery, new
                {
                    updatedRequest.RequestedItem,
                    updatedRequest.SuggestedDealer,
                    updatedRequest.Purpose,
                    updatedRequest.EstimatedCost,
                    updatedRequest.RequestedDate,
                    updatedRequest.Status,
                    updatedRequest.Priority,
                    RequestId = requestId
                });

                var updatedResult = await connection.QuerySingleOrDefaultAsync<RequestItem>(selectQuery, new { RequestId = requestId });
                return Ok(updatedResult);
            }
        }

        // API for deleting a request by RequestID
        [HttpDelete("DeleteRequest/{requestId}")]
        public async Task<IActionResult> DeleteRequest(int requestId)
        {
            string deleteQuery = @"
                DELETE FROM RequestItems_tb WHERE RequestID = @RequestId;
            ";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.ExecuteAsync(deleteQuery, new { RequestId = requestId });

                if (result > 0)
                {
                    return Ok($"Request with ID {requestId} was successfully deleted.");
                }
                else
                {
                    return NotFound($"Request with ID {requestId} was not found.");
                }
            }
        }

        // API for updating Admin3 approval and setting status based on approval, returning updated request

        [HttpGet("GetRequestsByBorrower/{borrowerId}")]
        public async Task<IActionResult> GetRequestsByBorrower(int borrowerId)
        {
            string selectQuery = @"
        SELECT RequestID, RequestedItem, RequestedBy, SuggestedDealer, Purpose, EstimatedCost, RequestedDate, Status, Priority, Admin1Approval, Admin2Approval, Admin3Approval, BorrowerId,Description,Email
        FROM RequestItems_tb
        WHERE BorrowerId = @BorrowerId  ORDER BY RequestID DESC;
    ";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var requestItems = await connection.QueryAsync<RequestItem>(selectQuery, new { BorrowerId = borrowerId });
                return Ok(requestItems);
            }
        }

        [HttpPut("CancelRequest/{requestId}")]
        public async Task<IActionResult> CancelRequestItem(int requestId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string query = @"
            UPDATE RequestItems_tb
            SET Status = 'Canceled'
            WHERE RequestID = @requestId";

                var rowsAffected = await connection.ExecuteAsync(query, new { requestId });

                if (rowsAffected == 0)
                {
                    return NotFound($"No request found with RequestID {requestId}.");
                }

                return Ok($"Request with RequestID {requestId} has been successfully canceled.");
            }
        }
        // API to reject a request with a reason

        // Admin1 Approved and Reject aproval
        [HttpPut("Admin1UpdateApproval/{requestId}")]
        public async Task<IActionResult> UpdateApproval(int requestId, [FromBody] UpdateApprovalRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Admin1Approval))
                return BadRequest("Invalid request. Admin1Approval must be provided.");

            if (request.Admin1Approval == "Declined" && (string.IsNullOrEmpty(request.RejectReason) || string.IsNullOrEmpty(request.RejectBy)))
                return BadRequest("RejectReason and RejectBy must be provided when rejecting.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if RequestID exists
                        string checkExistQuery = "SELECT COUNT(1) FROM RequestItems_tb WHERE RequestID = @RequestId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { RequestId = requestId });

                        if (!exists)
                            return NotFound($"Request with ID {requestId} not found.");

                        // Update Admin1Approval
                        string updateApprovalQuery = @"
                UPDATE RequestItems_tb 
                SET Admin1Approval = @Admin1Approval
                WHERE RequestID = @RequestId";

                        await connection.ExecuteAsync(updateApprovalQuery, new
                        {
                            Admin1Approval = request.Admin1Approval,
                            RequestId = requestId
                        }, transaction);

                        // If Admin1Approval is "Approved", update Status to "In Progress"
                        if (request.Admin1Approval == "Approved")
                        {
                            string updateStatusQuery = @"
                    UPDATE RequestItems_tb 
                    SET Status = 'In Progress'
                    WHERE RequestID = @RequestId";

                            await connection.ExecuteAsync(updateStatusQuery, new { RequestId = requestId }, transaction);
                        }
                        // If Admin1Approval is "Rejected", update Status, RejectReason, and RejectBy
                        else if (request.Admin1Approval == "Declined")
                        {
                            string updateRejectionQuery = @"
                    UPDATE RequestItems_tb 
                    SET Status = 'Rejected', 
                        RejectedReason = @RejectReason, 
                        RejectedBy = @RejectBy
                    WHERE RequestID = @RequestId";

                            await connection.ExecuteAsync(updateRejectionQuery, new
                            {
                                RequestId = requestId,
                                RejectReason = request.RejectReason,
                                RejectBy = request.RejectBy
                            }, transaction);
                        }

                        transaction.Commit();
                        return Ok("Approval and status updated successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Log the exception if logging is configured
                        // _logger.LogError(ex, "Error updating approval");
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }

        [HttpPut("UpdateAdmin2Approval/{requestId}")]
        public async Task<IActionResult> UpdateAdmin2Approval(int requestId, [FromBody] UpdateAdmin2ApprovalRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Admin2Approval))
                return BadRequest("Invalid request. Admin2Approval must be provided.");

            if (request.Admin2Approval == "Declined" && (string.IsNullOrEmpty(request.RejectReason) || string.IsNullOrEmpty(request.RejectBy)))
                return BadRequest("RejectReason and RejectBy must be provided when rejecting.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if RequestID exists
                        string checkExistQuery = "SELECT COUNT(1) FROM RequestItems_tb WHERE RequestID = @RequestId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { RequestId = requestId });

                        if (!exists)
                            return NotFound($"Request with ID {requestId} not found.");

                        // Update Admin2Approval
                        string updateApprovalQuery = @"
                    UPDATE RequestItems_tb 
                    SET Admin2Approval = @Admin2Approval
                    WHERE RequestID = @RequestId";

                        await connection.ExecuteAsync(updateApprovalQuery, new
                        {
                            Admin2Approval = request.Admin2Approval,
                            RequestId = requestId
                        }, transaction);

                        // Update status based on Admin2Approval and other conditions
                        if (request.Admin2Approval == "Approved")
                        {
                            string updateStatusQuery = @"
                        UPDATE RequestItems_tb 
                        SET Status = CASE 
                            WHEN Admin1Approval = 'Approved' THEN 'In Progress'
                            ELSE Status
                        END
                        WHERE RequestID = @RequestId";

                            await connection.ExecuteAsync(updateStatusQuery, new { RequestId = requestId }, transaction);
                        }
                        else if (request.Admin2Approval == "Declined")
                        {
                            string updateRejectionQuery = @"
                        UPDATE RequestItems_tb 
                        SET Status = 'Rejected', 
                            RejectedReason = @RejectReason, 
                            RejectedBy = @RejectBy
                        WHERE RequestID = @RequestId";

                            await connection.ExecuteAsync(updateRejectionQuery, new
                            {
                                RequestId = requestId,
                                RejectReason = request.RejectReason,
                                RejectBy = request.RejectBy
                            }, transaction);
                        }

                        // Fetch and return the updated record
                        string selectQuery = @"
                    SELECT * FROM RequestItems_tb WHERE RequestID = @RequestId";
                        var updatedRequest = await connection.QuerySingleOrDefaultAsync<RequestItem>(selectQuery, new { RequestId = requestId }, transaction);

                        transaction.Commit();
                        return Ok(updatedRequest);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Log the exception if logging is configured
                        // _logger.LogError(ex, "Error updating Admin2 approval");
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }

        [HttpPut("UpdateAdmin3Approval/{requestId}")]
        public async Task<IActionResult> UpdateAdmin3Approval(int requestId, [FromBody] UpdateAdmin3ApprovalRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Admin3Approval))
                return BadRequest("Invalid request. Admin3Approval must be provided.");

            if (request.Admin3Approval == "Declined" && (string.IsNullOrEmpty(request.RejectReason) || string.IsNullOrEmpty(request.RejectBy)))
                return BadRequest("RejectReason and RejectBy must be provided when rejecting.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if RequestID exists
                        string checkExistQuery = "SELECT COUNT(1) FROM RequestItems_tb WHERE RequestID = @RequestId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { RequestId = requestId });

                        if (!exists)
                            return NotFound($"Request with ID {requestId} not found.");

                        // Update Admin3Approval
                        string updateApprovalQuery = @"
                    UPDATE RequestItems_tb 
                    SET Admin3Approval = @Admin3Approval
                    WHERE RequestID = @RequestId";

                        await connection.ExecuteAsync(updateApprovalQuery, new
                        {
                            Admin3Approval = request.Admin3Approval,
                            RequestId = requestId
                        }, transaction);

                        // Update status based on Admin3Approval and other conditions
                        if (request.Admin3Approval == "Approved")
                        {
                            string updateStatusQuery = @"
                        UPDATE RequestItems_tb 
                        SET Status = CASE 
                            WHEN Admin1Approval = 'Approved' AND Admin2Approval = 'Approved' THEN 'Approved'
                            ELSE Status
                        END
                        WHERE RequestID = @RequestId";

                            await connection.ExecuteAsync(updateStatusQuery, new { RequestId = requestId }, transaction);
                        }
                        else if (request.Admin3Approval == "Declined")
                        {
                            string updateRejectionQuery = @"
                        UPDATE RequestItems_tb 
                        SET Status = 'Rejected', 
                            RejectedReason = @RejectReason, 
                            RejectedBy = @RejectBy
                        WHERE RequestID = @RequestId";

                            await connection.ExecuteAsync(updateRejectionQuery, new
                            {
                                RequestId = requestId,
                                RejectReason = request.RejectReason,
                                RejectBy = request.RejectBy
                            }, transaction);
                        }

                        // Fetch and return the updated record
                        string selectQuery = @"
                    SELECT * FROM RequestItems_tb WHERE RequestID = @RequestId";
                        var updatedRequest = await connection.QuerySingleOrDefaultAsync<RequestItem>(selectQuery, new { RequestId = requestId }, transaction);

                        transaction.Commit();
                        return Ok(updatedRequest);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Log the exception if logging is configured
                        // _logger.LogError(ex, "Error updating Admin3 approval");
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }


    }





}
public class UpdateApprovalRequest
{

    public string Admin1Approval { get; set; }
    public string RejectReason { get; set; }
    public string RejectBy { get; set; }
}

public class UpdateAdmin2ApprovalRequest
{

    public string Admin2Approval { get; set; }
    public string RejectReason { get; set; }
    public string RejectBy { get; set; }
}

public class UpdateAdmin3ApprovalRequest
{
    public string Admin3Approval { get; set; }
    public string RejectReason { get; set; }
    public string RejectBy { get; set; }
}