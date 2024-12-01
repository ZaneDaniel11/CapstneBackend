using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using System.Collections.Generic;
using Maintenace.Models;
using System;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";


        [HttpGet("GetAllMaintenanceRequest")]
        public async Task<IActionResult> GetAllReqMaintenaceAsync()
        {
            const string query = "SELECT * FROM maintenance_tb";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var maintenance = await connection.QueryAsync<Maintenance>(query);
                return Ok(maintenance); // Return filtered assets based on CategoryID
            }
        }


        [HttpGet("GetMaintenanceByRequester/{requesterId}")]
        public async Task<IActionResult> GetMaintenanceByRequesterAsync(int requesterId)
        {
            const string query = "SELECT * FROM maintenance_tb WHERE RequesterID = @RequesterID  ORDER BY MaintenanceID DESC";

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var maintenanceRequests = await connection.QueryAsync<Maintenance>(query, new { RequesterID = requesterId });
                return Ok(maintenanceRequests); // Return maintenance requests for the given RequesterID
            }
        }


        [HttpPost("InsertMaintenance")]
        public async Task<IActionResult> InsertMaintenanceAsync(Maintenance newMaintenance)
        {
            const string insertQuery = @"
        INSERT INTO maintenance_tb 
        (ItemID, CategoryID, AssetName, AssetCode, Location, Issue, RequestDate, RequestedBy, 
         RequesterID, MaintenanceStatus, AssignedTo, ScheduledDate, CompletionDate, Description, 
         ApprovalStatus, ApprovedByAdmin1, ApprovedByAdmin2) 
        VALUES 
        (@ItemID, @CategoryID, @AssetName, @AssetCode, @Location, @Issue, @RequestDate, @RequestedBy, 
         @RequesterID, @MaintenanceStatus, @AssignedTo, @ScheduledDate, @CompletionDate, @Description, 
         @ApprovalStatus, @ApprovedByAdmin1, @ApprovedByAdmin2);
        SELECT * FROM maintenance_tb ORDER BY MaintenanceID DESC LIMIT 1;";

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Insert maintenance request and retrieve the newly created record
                var result = await connection.QuerySingleOrDefaultAsync<Maintenance>(insertQuery, new
                {
                    newMaintenance.ItemID,
                    newMaintenance.CategoryID,
                    newMaintenance.AssetName,
                    newMaintenance.AssetCode,
                    newMaintenance.Location,
                    newMaintenance.Issue,
                    newMaintenance.RequestDate,
                    newMaintenance.RequestedBy,
                    newMaintenance.RequesterID,
                    newMaintenance.MaintenanceStatus,
                    newMaintenance.AssignedTo,
                    newMaintenance.ScheduledDate,
                    newMaintenance.CompletionDate,
                    newMaintenance.Description,
                    newMaintenance.ApprovalStatus,
                    newMaintenance.ApprovedByAdmin1,
                    newMaintenance.ApprovedByAdmin2
                });

                return Ok(result); // Return the newly inserted maintenance record
            }
        }
        [HttpPut("Admin1UpdateApproval/{maintenanceId}")]
        public async Task<IActionResult> Admin1UpdateApprovalAsync(int maintenanceId, [FromBody] MaintenanceAdmin1Aproval request)
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
                        // Check if MaintenanceID exists
                        string checkExistQuery = "SELECT COUNT(1) FROM maintenance_tb WHERE MaintenanceID = @MaintenanceId";
                        var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { MaintenanceId = maintenanceId });

                        if (!exists)
                            return NotFound($"Maintenance request with ID {maintenanceId} not found.");

                        // Update Admin1Approval
                        string updateApprovalQuery = @"
                UPDATE maintenance_tb 
                SET ApprovedByAdmin1 = @Admin1Approval
                WHERE MaintenanceID = @MaintenanceId";

                        await connection.ExecuteAsync(updateApprovalQuery, new
                        {
                            Admin1Approval = request.Admin1Approval,
                            MaintenanceId = maintenanceId
                        }, transaction);

                        // If Admin1Approval is "Approved", update ApprovalStatus to "In Progress"
                        if (request.Admin1Approval == "Approved")
                        {
                            string updateStatusQuery = @"
                    UPDATE maintenance_tb 
                    SET ApprovalStatus = 'In Progress'
                    WHERE MaintenanceID = @MaintenanceId";

                            await connection.ExecuteAsync(updateStatusQuery, new { MaintenanceId = maintenanceId }, transaction);
                        }
                        // If Admin1Approval is "Declined", update ApprovalStatus, RejectReason, and RejectBy
                        else if (request.Admin1Approval == "Declined")
                        {
                            string updateRejectionQuery = @"
                    UPDATE maintenance_tb 
                    SET ApprovalStatus = 'Rejected', 
                        RejectReason = @RejectReason, 
                        RejectBy = @RejectBy
                    WHERE MaintenanceID = @MaintenanceId";

                            await connection.ExecuteAsync(updateRejectionQuery, new
                            {
                                MaintenanceId = maintenanceId,
                                RejectReason = request.RejectReason,
                                RejectBy = request.RejectBy
                            }, transaction);
                        }

                        transaction.Commit();
                        return Ok("Admin1 approval and status updated successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }
                }
            }
        }

        [HttpPut("Admin2UpdateApproval/{maintenanceId}")]
public async Task<IActionResult> Admin2UpdateApprovalAsync(int maintenanceId, [FromBody] MaintenanceAdmin2Approval request)
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
                // Check if MaintenanceID exists
                string checkExistQuery = "SELECT COUNT(1) FROM maintenance_tb WHERE MaintenanceID = @MaintenanceId";
                var exists = await connection.ExecuteScalarAsync<bool>(checkExistQuery, new { MaintenanceId = maintenanceId });

                if (!exists)
                    return NotFound($"Maintenance request with ID {maintenanceId} not found.");

                // Update Admin2Approval
                string updateApprovalQuery = @"
            UPDATE maintenance_tb 
            SET ApprovedByAdmin2 = @Admin2Approval
            WHERE MaintenanceID = @MaintenanceId";

                await connection.ExecuteAsync(updateApprovalQuery, new
                {
                    Admin2Approval = request.Admin2Approval,
                    MaintenanceId = maintenanceId
                }, transaction);

                // If Admin2Approval is "Approved", update ApprovalStatus to "In Progress"
                if (request.Admin2Approval == "Approved")
                {
                    string updateStatusQuery = @"
                UPDATE maintenance_tb 
                SET ApprovalStatus = 'Approved'
                WHERE MaintenanceID = @MaintenanceId";

                    await connection.ExecuteAsync(updateStatusQuery, new { MaintenanceId = maintenanceId }, transaction);
                }
                // If Admin2Approval is "Declined", update ApprovalStatus, RejectReason, and RejectBy
                else if (request.Admin2Approval == "Declined")
                {
                    string updateRejectionQuery = @"
                UPDATE maintenance_tb 
                SET ApprovalStatus = 'Rejected', 
                    RejectReason = @RejectReason, 
                    RejectBy = @RejectBy
                WHERE MaintenanceID = @MaintenanceId";

                    await connection.ExecuteAsync(updateRejectionQuery, new
                    {
                        MaintenanceId = maintenanceId,
                        RejectReason = request.RejectReason,
                        RejectBy = request.RejectBy
                    }, transaction);
                }

                transaction.Commit();
                return Ok("Admin2 approval and status updated successfully.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}



    }

}
