using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using Categorys.Models;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BorrowRequestApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";

        [HttpGet("RequestsByBorrowerId")]
        public async Task<IActionResult> GetRequestsByBorrowerId([FromQuery] int borrowerId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                        SELECT br.BorrowId, br.ReqBorrowDate, br.RequestedBy, br.Purpose, br.Status, br.Priority, bi.ItemName, bi.Quantity
                        FROM Borrowreq_tb br
                        JOIN BorrowItems_tb bi ON br.BorrowId = bi.BorrowId
                        WHERE br.BorrowerId = @BorrowerId";

                    var borrowerRequests = await connection.QueryAsync(query, new { BorrowerId = borrowerId });

                    if (!borrowerRequests.Any())
                        return NotFound("No borrow requests found for this borrower.");

                    return Ok(borrowerRequests);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("Request")]
        public async Task<IActionResult> BorrowRequest([FromBody] BorrowRequest borrowRequest)
        {
            if (borrowRequest == null || !borrowRequest.Items.Any())
                return BadRequest("No items selected for borrowing.");

            var borrowDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Insert into Borrowreq_tb once
                        const string insertBorrowRequestQuery = @"
                    INSERT INTO Borrowreq_tb (ReqBorrowDate, RequestedBy, Purpose, Status, Priority, BorrowerId, Email) 
                    VALUES (@ReqBorrowDate, @RequestedBy, @Purpose, @Status, @Priority, @BorrowerId, @Email); 
                    SELECT last_insert_rowid();";

                        // Insert into Borrowreq_tb and get the generated BorrowId
                        var borrowId = await connection.ExecuteScalarAsync<int>(insertBorrowRequestQuery, new
                        {
                            ReqBorrowDate = borrowDate,
                            RequestedBy = borrowRequest.RequestedBy,
                            Purpose = borrowRequest.Purpose,
                            Status = borrowRequest.Status,
                            Priority = borrowRequest.Priority,
                            BorrowerId = borrowRequest.BorrowerId,
                            Email = borrowRequest.Email // Include Email here
                        }, transaction);

                        // Prepare the query for inserting into BorrowItems_tb
                        const string insertBorrowItemsQuery = @"
                    INSERT INTO BorrowItems_tb (BorrowId, ItemID, CategoryID, ItemName, Quantity) 
                    VALUES (@BorrowId, @ItemID, @CategoryID, @ItemName, @Quantity)";

                        const string updateItemQuantityQuery = @"
                    UPDATE items_db 
                    SET Quantity = Quantity - @Quantity 
                    WHERE ItemID = @ItemID AND Quantity >= @Quantity";

                        // Loop through the items and insert into BorrowItems_tb, then update quantities
                        foreach (var item in borrowRequest.Items)
                        {
                            // Insert each item into BorrowItems_tb
                            await connection.ExecuteAsync(insertBorrowItemsQuery, new
                            {
                                BorrowId = borrowId,
                                ItemID = item.ItemID,
                                CategoryID = item.CategoryID,
                                ItemName = item.ItemName,
                                Quantity = item.Quantity
                            }, transaction);

                            // Update the quantity in items_db
                            var rowsAffected = await connection.ExecuteAsync(updateItemQuantityQuery, new
                            {
                                ItemID = item.ItemID,
                                Quantity = item.Quantity
                            }, transaction);

                            // Rollback if not enough stock
                            if (rowsAffected == 0)
                            {
                                transaction.Rollback();
                                return BadRequest($"Not enough stock for {item.ItemName}.");
                            }
                        }

                        // Commit the transaction once after all items and borrow request are inserted/updated
                        transaction.Commit();
                        return Ok(new { BorrowId = borrowId });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }

        // GET: api/Borrow/ViewRequest/{borrowId}
        [HttpGet("ViewRequest/{borrowId}")]
        public async Task<IActionResult> ViewRequest(int borrowId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string query = @"
                    SELECT br.ReqBorrowDate, br.RequestedBy, br.Purpose, br.Status, br.Priority, bi.ItemName, bi.Quantity
                    FROM Borrowreq_tb br
                    JOIN BorrowItems_tb bi ON br.BorrowId = bi.BorrowId
                    WHERE br.BorrowId = @BorrowId";

                var borrowDetails = await connection.QueryAsync(query, new { BorrowId = borrowId });

                if (!borrowDetails.Any())
                    return NotFound("Borrow request not found.");

                return Ok(borrowDetails);
            }
        }

        // GET: api/Borrow/AllRequests
        [HttpGet("AllRequests")]
        public async Task<IActionResult> GetAllRequests()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string query = "SELECT * FROM Borrowreq_tb";

                var allRequests = await connection.QueryAsync(query);

                if (!allRequests.Any())
                    return NotFound("No borrow requests found.");

                return Ok(allRequests);
            }
        }

        [HttpGet("RequestById/{borrowerId}")]
        public async Task<IActionResult> GetRequestsByBorrowerId1(int borrowerId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string query = "SELECT * FROM Borrowreq_tb WHERE BorrowerId = @borrowerId  ORDER BY BorrowId DESC";

                var requests = await connection.QueryAsync(query, new { borrowerId });

                if (!requests.Any())
                    return NotFound($"No borrow requests found for BorrowerId {borrowerId}.");

                return Ok(requests);
            }
        }

        [HttpPut("UpdateStatus/{borrowId}")]
        public async Task<IActionResult> UpdateBorrowRequestStatus(int borrowId, [FromBody] string newStatus)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string query = @"
            UPDATE Borrowreq_tb 
            SET Status = @newStatus 
            WHERE BorrowId = @borrowId";

                var rowsAffected = await connection.ExecuteAsync(query, new { borrowId, newStatus });

                if (rowsAffected == 0)
                {
                    return NotFound($"No borrow request found with BorrowId {borrowId}.");
                }

                return Ok($"Borrow request with BorrowId {borrowId} successfully updated to status '{newStatus}'.");
            }
        }


        // GET: api/Borrow/RequestsByBorrower/{borrowerId}
        [HttpGet("RequestsByBorrower/{borrowerId}")]
        public async Task<IActionResult> GetRequestsByBorrower(int borrowerId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                Console.WriteLine($"Fetching requests for borrowerId: {borrowerId}");  // Add this log

                const string query = @"
            SELECT br.BorrowId, br.ReqBorrowDate, br.RequestedBy, br.Purpose, br.Status, br.Priority, bi.ItemName, bi.Quantity
            FROM Borrowreq_tb br
            JOIN BorrowItems_tb bi ON br.BorrowId = bi.BorrowId
            WHERE br.BorrowerId = @BorrowerId";

                var borrowRequests = await connection.QueryAsync(query, new { BorrowerId = borrowerId });

                if (!borrowRequests.Any())
                    return NotFound("No borrow requests found for this borrower.");

                return Ok(borrowRequests);
            }
        }

        [HttpPut("UpdateApproval/{borrowId}")]
        public async Task<IActionResult> UpdateApproval(int borrowId, [FromBody] UpdateApprovalRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Admin1Approval))
                return BadRequest("Invalid request. Admin1Approval must be provided.");

            if (request.Admin1Approval == "Rejected" && (string.IsNullOrEmpty(request.RejectReason) || string.IsNullOrEmpty(request.RejectBy)))
                return BadRequest("RejectReason and RejectBy must be provided when rejecting.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if BorrowId exists
                        string checkExistQuery = "SELECT COUNT(1) FROM Borrowreq_tb WHERE BorrowId = @BorrowId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { BorrowId = borrowId });

                        if (!exists)
                            return NotFound($"Borrow request with ID {borrowId} not found.");

                        // Update Admin1Approval
                        string updateApprovalQuery = @"
                UPDATE Borrowreq_tb 
                SET Admin1Approval = @Admin1Approval
                WHERE BorrowId = @BorrowId";

                        await connection.ExecuteAsync(updateApprovalQuery, new
                        {
                            Admin1Approval = request.Admin1Approval,
                            BorrowId = borrowId
                        }, transaction);

                        // If Admin1Approval is "Approved", update Status to "In Progress"
                        if (request.Admin1Approval == "Approved")
                        {
                            string updateStatusQuery = @"
                    UPDATE Borrowreq_tb 
                    SET Status = 'In Progress'
                    WHERE BorrowId = @BorrowId";

                            await connection.ExecuteAsync(updateStatusQuery, new { BorrowId = borrowId }, transaction);
                        }
                        // If Admin1Approval is "Rejected", update Status, RejectReason, and RejectBy
                        else if (request.Admin1Approval == "Declined")
                        {
                            string updateRejectionQuery = @"
                    UPDATE Borrowreq_tb 
                    SET Status = 'Declined', 
                        RejectReason = @RejectReason, 
                        RejectBy = @RejectBy
                    WHERE BorrowId = @BorrowId";

                            await connection.ExecuteAsync(updateRejectionQuery, new
                            {
                                BorrowId = borrowId,
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

        [HttpPut("UpdateApprovalAdmin2/{borrowId}")]
        public async Task<IActionResult> UpdateApprovalAdmin2(int borrowId, [FromBody] UpdateApprovalRequestAdmin2 request)
        {
            if (request == null || string.IsNullOrEmpty(request.Admin2Approval))
                return BadRequest("Invalid request. Admin2Approval must be provided.");

            if (request.Admin2Approval == "Rejected" && (string.IsNullOrEmpty(request.RejectReason) || string.IsNullOrEmpty(request.RejectBy)))
                return BadRequest("RejectReason and RejectBy must be provided when rejecting.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if BorrowId exists
                        string checkExistQuery = "SELECT COUNT(1) FROM Borrowreq_tb WHERE BorrowId = @BorrowId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { BorrowId = borrowId });

                        if (!exists)
                            return NotFound($"Borrow request with ID {borrowId} not found.");

                        // Update Admin2Approval
                        string updateApprovalQuery = @"
                UPDATE Borrowreq_tb 
                SET Admin2Approval = @Admin2Approval
                WHERE BorrowId = @BorrowId";

                        await connection.ExecuteAsync(updateApprovalQuery, new
                        {
                            Admin2Approval = request.Admin2Approval,
                            BorrowId = borrowId
                        }, transaction);

                        // If Admin2Approval is "Approved", update Status to "In Progress"
                        if (request.Admin2Approval == "Approved")
                        {
                            string updateStatusQuery = @"
                    UPDATE Borrowreq_tb 
                    SET Status = 'In Progress'
                    WHERE BorrowId = @BorrowId";

                            await connection.ExecuteAsync(updateStatusQuery, new { BorrowId = borrowId }, transaction);
                        }
                        // If Admin2Approval is "Rejected", update Status, RejectReason, and RejectBy
                        else if (request.Admin2Approval == "Declined")
                        {
                            string updateRejectionQuery = @"
                    UPDATE Borrowreq_tb 
                    SET Status = 'Rejected', 
                        RejectReason = @RejectReason, 
                        RejectBy = @RejectBy
                    WHERE BorrowId = @BorrowId";

                            await connection.ExecuteAsync(updateRejectionQuery, new
                            {
                                BorrowId = borrowId,
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
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }


        [HttpGet("ApprovedByAdmin1")]
        public async Task<IActionResult> GetApprovedByAdmin1Requests()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // SQL query to fetch only the requests where Admin1 has approved
                const string query = @"
            SELECT br.BorrowId, br.ReqBorrowDate, br.RequestedBy, br.Purpose, br.Status, br.Priority, br.Admin1Approval, br.Admin2Approval
            FROM Borrowreq_tb br
            WHERE br.Admin1Approval = 'Approved'";  // Check for Admin1 approval

                var approvedRequests = await connection.QueryAsync(query);

                if (!approvedRequests.Any())
                    return NotFound("No borrow requests approved by Admin1 found.");

                return Ok(approvedRequests);
            }
        }

        [HttpGet("ApprovedByBothAdmins")]
        public async Task<IActionResult> GetApprovedByBothAdminsRequests()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // SQL query to fetch requests approved by both Admin1 and Admin2
                const string query = @"
            SELECT br.BorrowId, br.ReqBorrowDate, br.RequestedBy, br.Purpose, br.Status, br.Priority, br.Admin1Approval, br.Admin2Approval,br.Admin3Approval, bi.ItemName, bi.Quantity,br.Email
            FROM Borrowreq_tb br
            JOIN BorrowItems_tb bi ON br.BorrowId = bi.BorrowId
            WHERE br.Admin1Approval = 'Approved' AND br.Admin2Approval = 'Approved'"; // Check for both approvals

                var approvedRequests = await connection.QueryAsync(query);

                if (!approvedRequests.Any())
                    return NotFound("No borrow requests approved by both Admin1 and Admin2 found.");

                return Ok(approvedRequests);
            }
        }

        [HttpPut("UpdateApprovalAdmin3/{borrowId}")]
        public async Task<IActionResult> UpdateApprovalAdmin3(int borrowId, [FromBody] UpdateApprovalRequestAdmin3 request)
        {
            if (request == null || string.IsNullOrEmpty(request.Admin3Approval))
                return BadRequest("Invalid request. Admin3Approval must be provided.");

            if (request.Admin3Approval == "Rejected" && (string.IsNullOrEmpty(request.RejectReason) || string.IsNullOrEmpty(request.RejectBy)))
                return BadRequest("RejectReason and RejectBy must be provided when rejecting.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if BorrowId exists
                        string checkExistQuery = "SELECT COUNT(1) FROM Borrowreq_tb WHERE BorrowId = @BorrowId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { BorrowId = borrowId });

                        if (!exists)
                            return NotFound($"Borrow request with ID {borrowId} not found.");

                        // Update Admin3Approval
                        string updateApprovalQuery = @"
            UPDATE Borrowreq_tb 
            SET Admin3Approval = @Admin3Approval
            WHERE BorrowId = @BorrowId";

                        await connection.ExecuteAsync(updateApprovalQuery, new
                        {
                            Admin3Approval = request.Admin3Approval,
                            BorrowId = borrowId
                        }, transaction);

                        // If Admin3Approval is "Approved", update Status to "Approved" and ReturnStatus to "Not Returned"
                        if (request.Admin3Approval == "Approved")
                        {
                            string updateStatusQuery = @"
                UPDATE Borrowreq_tb 
                SET Status = 'Approved',
                    RecieveStatus = 'Not Recieve',
                    ReturnStatus = 'Not Returned'
                WHERE BorrowId = @BorrowId";

                            await connection.ExecuteAsync(updateStatusQuery, new { BorrowId = borrowId }, transaction);
                        }
                        // If Admin3Approval is "Rejected", update Status, RejectReason, and RejectBy
                        else if (request.Admin3Approval == "Declined")
                        {
                            string updateRejectionQuery = @"
                UPDATE Borrowreq_tb 
                SET Status = 'Rejected', 
                    RejectReason = @RejectReason, 
                    RejectBy = @RejectBy
                WHERE BorrowId = @BorrowId";

                            await connection.ExecuteAsync(updateRejectionQuery, new
                            {
                                BorrowId = borrowId,
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
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }




        [HttpPut("UpdateReturnStatus/{borrowId}")]
        public async Task<IActionResult> UpdateReturnStatus(int borrowId, [FromBody] UpdateReturnStatusRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ReturnStatus))
                return BadRequest("Invalid request. ReturnStatus must be provided.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Update only the ReturnStatus field
                        string updateReturnStatusQuery = @"
                    UPDATE Borrowreq_tb 
                    SET ReturnStatus = @ReturnStatus
                    WHERE BorrowId = @BorrowId";

                        await connection.ExecuteAsync(updateReturnStatusQuery, new
                        {
                            ReturnStatus = request.ReturnStatus,
                            BorrowId = borrowId
                        }, transaction);

                        // If the ReturnStatus is "Returned", update the quantity in items_db
                        if (request.ReturnStatus == "Returned")
                        {
                            // Query to get borrowed items with their quantities
                            string selectBorrowedItemsQuery = @"
                        SELECT ItemID, Quantity 
                        FROM BorrowItems_tb 
                        WHERE BorrowId = @BorrowId";

                            var borrowedItems = await connection.QueryAsync(selectBorrowedItemsQuery, new
                            {
                                BorrowId = borrowId
                            }, transaction);

                            // Update each item's quantity in items_db
                            foreach (var item in borrowedItems)
                            {
                                string updateItemQuantityQuery = @"
                            UPDATE items_db 
                            SET Quantity = Quantity + @Quantity
                            WHERE ItemID = @ItemID";

                                await connection.ExecuteAsync(updateItemQuantityQuery, new
                                {
                                    Quantity = item.Quantity,
                                    ItemID = item.ItemID
                                }, transaction);
                            }
                        }

                        transaction.Commit();
                        return Ok("Return status and item quantities updated successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }
        [HttpPost("CancelRequest")]
        public async Task<IActionResult> CancelBorrowRequest([FromBody] BorrowRequestDto request)
        {
            if (request?.BorrowId <= 0)
                return BadRequest("Invalid Borrow Request ID.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check if BorrowId exists
                        string checkExistQuery = "SELECT COUNT(1) FROM Borrowreq_tb WHERE BorrowId = @BorrowId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { BorrowId = request.BorrowId });

                        if (!exists)
                            return NotFound($"Borrow request with ID {request.BorrowId} not found.");

                        // Update item quantities back to inventory
                        const string updateItemsSql = @"
                WITH CanceledItems AS (
                    SELECT bi.ItemID, bi.Quantity AS BorrowedQuantity
                    FROM BorrowItems_tb bi
                    JOIN Borrowreq_tb br ON bi.BorrowId = br.BorrowId
                    WHERE br.Status != 'Canceled' AND br.BorrowId = @BorrowId
                )
                UPDATE items_db
                SET Quantity = Quantity + (
                    SELECT BorrowedQuantity FROM CanceledItems WHERE CanceledItems.ItemID = items_db.ItemID
                )
                WHERE ItemID IN (SELECT ItemID FROM CanceledItems);";

                        await connection.ExecuteAsync(updateItemsSql, new { BorrowId = request.BorrowId }, transaction);

                        // Update BorrowReq_tb status to "Canceled"
                        const string cancelRequestSql = @"
                UPDATE Borrowreq_tb
                SET Status = 'Canceled'
                WHERE BorrowId = @BorrowId AND Status != 'Canceled';";

                        var rowsAffected = await connection.ExecuteAsync(cancelRequestSql, new { BorrowId = request.BorrowId }, transaction);

                        if (rowsAffected == 0)
                        {
                            transaction.Rollback();
                            return NotFound($"Borrow request with ID {request.BorrowId} not found or already canceled.");
                        }

                        // Update Admin1Approval to "Canceled"
                        const string updateApprovalSql = @"
                UPDATE Borrowreq_tb
                SET Admin1Approval = 'Canceled'
                WHERE BorrowId = @BorrowId;";

                        await connection.ExecuteAsync(updateApprovalSql, new { BorrowId = request.BorrowId }, transaction);

                        transaction.Commit();

                        // Return the response with BorrowId
                        return Ok(new { BorrowId = request.BorrowId });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }

        [HttpPost("InsertNote")]
        public async Task<IActionResult> InsertNote([FromBody] NoteRequest noteRequest)
        {
            if (noteRequest == null || noteRequest.BorrowId <= 0 || string.IsNullOrWhiteSpace(noteRequest.Note))
                return BadRequest("Invalid BorrowId or Note.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    const string query = @"
                UPDATE Borrowreq_tb 
                SET Note = @Note 
                WHERE BorrowId = @BorrowId";

                    var rowsAffected = await connection.ExecuteAsync(query, new
                    {
                        Note = noteRequest.Note,
                        BorrowId = noteRequest.BorrowId
                    });

                    if (rowsAffected == 0)
                        return NotFound("BorrowId not found.");

                    return Ok("Note successfully added.");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
        }




        [HttpPost("UpdateRecieveStatus")]
        public async Task<IActionResult> UpdateRecieveStatus([FromBody] RecieveStatusRequest recieveStatusRequest)
        {
            if (recieveStatusRequest == null || recieveStatusRequest.BorrowID <= 0 || string.IsNullOrWhiteSpace(recieveStatusRequest.RecieveStatus))
                return BadRequest("Invalid BorrowerID or RecieveStatus.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    const string query = @"
                UPDATE Borrowreq_tb 
                SET RecieveStatus = @RecieveStatus 
                WHERE BorrowID = @BorrowID";

                    var rowsAffected = await connection.ExecuteAsync(query, new
                    {
                        RecieveStatus = recieveStatusRequest.RecieveStatus,
                        BorrowID = recieveStatusRequest.BorrowID
                    });

                    if (rowsAffected == 0)
                        return NotFound("BorrowerID not found.");

                    return Ok("RecieveStatus successfully updated.");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
        }



    }
    public class RecieveStatusRequest
    {
        public int BorrowID { get; set; }
        public string RecieveStatus { get; set; }
    }


    public class NoteRequest
    {
        public int BorrowId { get; set; }
        public string Note { get; set; }
    }

    public class UpdateReturnStatusRequest
    {
        public string ReturnStatus { get; set; }
    }

    // Model for Borrow Request
    public class BorrowRequest
    {
        public string RequestedBy { get; set; }
        public string Purpose { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public int BorrowerId { get; set; }
        public string Email { get; set; }
        public List<BorrowItem> Items { get; set; }
    }

    // Model for Borrowed Items
    public class BorrowItem
    {
        public int ItemID { get; set; }      // New field for ItemID
        public int CategoryID { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateApprovalRequest
    {
        public string Admin1Approval { get; set; }
        public string RejectReason { get; set; }
        public string RejectBy { get; set; }
    }


    public class UpdateApprovalRequestAdmin2
    {
        public string Admin2Approval { get; set; }
        public string RejectReason { get; set; }
        public string RejectBy { get; set; }
    }


    public class UpdateApprovalRequestAdmin3
    {
        public string Admin3Approval { get; set; }
        public string RejectReason { get; set; }
        public string RejectBy { get; set; }
    }
    // public class BorrowRequestResponse
    // {
    //     public int BorrowId { get; set; }
    // }

    public class BorrowRequestDto
    {
        public int BorrowId { get; set; }
    }


}




