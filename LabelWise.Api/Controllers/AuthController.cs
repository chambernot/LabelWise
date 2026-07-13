using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var result = await _auth.RegisterAsync(request);
                return CreatedAtAction(nameof(Register), result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _auth.LoginAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }
    }
}
