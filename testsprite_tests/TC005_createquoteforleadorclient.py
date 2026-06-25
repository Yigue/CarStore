import requests
import time

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
CARS_URL = f"{BASE_URL}/api/v1/cars"
LEADS_URL = f"{BASE_URL}/api/v1/leads"
QUOTES_URL = f"{BASE_URL}/api/v1/quotes"

EMAIL = "admin@carstore.com"
PASSWORD = "Admin123!"

def test_create_quote_for_lead_or_client():
    timeout = 30
    headers = {"Content-Type": "application/json"}

    # Step 1: Login and get token
    login_resp = requests.post(
        LOGIN_URL,
        json={"email": EMAIL, "password": PASSWORD},
        timeout=timeout
    )
    assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
    token = login_resp.json().get("token")
    assert token and isinstance(token, str), "No token returned from login"
    auth_headers = {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}

    # Step 2: Get a valid car ID from GET /api/v1/cars
    cars_resp = requests.get(CARS_URL, headers=auth_headers, timeout=timeout)
    assert cars_resp.status_code == 200, f"Failed to fetch cars: {cars_resp.text}"
    cars_data = cars_resp.json()
    items = cars_data.get("items")
    assert isinstance(items, list) and len(items) > 0, "No cars found in the response"
    car_id = items[0].get("id")
    assert car_id and isinstance(car_id, str), "Invalid car ID"

    # Step 3: Create a new lead (required to create quote for lead)
    lead_payload = {
        "clientName": "John Doe",
        "email": "johndoe@example.com",
        "phone": "1234567890",
        "source": 0,
        "notes": "Test notes",
        "interestedVehicleId": None
    }
    lead_resp = requests.post(LEADS_URL, json=lead_payload, headers=auth_headers, timeout=timeout)
    assert lead_resp.status_code == 201, f"Lead creation failed: {lead_resp.text}"
    lead_id = lead_resp.json().get("id")
    assert lead_id and isinstance(lead_id, str), "No lead ID returned"

    # Create the quote for the lead and verify creation and Pending status
    quote_id = None
    try:
        quote_payload = {
            "carId": car_id,
            "clientId": None,
            "leadId": lead_id,
            "proposedPrice": 15000.0,
            "paymentMethod": 0,
            "validUntil": "2026-07-20T00:00:00Z",
            "comments": "Quote comments"
        }
        quote_resp = requests.post(QUOTES_URL, json=quote_payload, headers=auth_headers, timeout=timeout)
        assert quote_resp.status_code == 201, f"Quote creation failed: {quote_resp.text}"
        quote_id = quote_resp.json().get("id")
        assert quote_id and isinstance(quote_id, str), "No quote ID returned"

        # Verify quote status is Pending
        # As the PRD does not define a direct GET for quote, assuming GET /api/v1/quotes/{id} exists or listing quotes
        # Since not defined, this part is limited to creation confirmation only per spec.
        # If GET endpoint existed, we would fetch and assert status == "Pending"

    finally:
        # Cleanup: Delete the lead created to keep system clean
        # API for deleting lead not specified, so no delete implemented here.
        # If lead deletion exists (e.g., DELETE /api/v1/leads/{id}), it should be called here to cleanup.
        pass

# test_create_quote_for_lead_or_client()