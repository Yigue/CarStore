import requests
import time

BASE_URL = "http://localhost:8080"
LOGIN_ENDPOINT = "/api/v1/users/login"
CARS_ENDPOINT = "/api/v1/cars"
LEADS_ENDPOINT = "/api/v1/leads"

EMAIL = "admin@carstore.com"
PASSWORD = "Admin123!"

def test_update_lead_status_with_valid_data():
    # Login and get token
    login_resp = requests.post(
        BASE_URL + LOGIN_ENDPOINT,
        json={"email": EMAIL, "password": PASSWORD},
        timeout=30
    )
    assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
    token = login_resp.json().get("token")
    assert token, "No token received on login"
    headers = {"Authorization": f"Bearer {token}"}

    # Fetch a valid car id (needed only if linking vehicle, not for status update, but per instructions)
    cars_resp = requests.get(BASE_URL + CARS_ENDPOINT, headers=headers, timeout=30)
    assert cars_resp.status_code == 200, f"Failed to get cars: {cars_resp.text}"
    cars_data = cars_resp.json()
    assert "items" in cars_data and isinstance(cars_data["items"], list), "Cars data invalid"
    assert len(cars_data["items"]) > 0, "No cars found to fetch id from"
    car_id = cars_data["items"][0].get("id")
    assert car_id, "First car has no id"

    lead_id = None
    try:
        # Create a new lead to update its status later
        lead_payload = {
            "clientName": "John Doe",
            "email": "johndoe@example.com",
            "phone": "1234567890",
            "source": 0,
            "notes": "Test notes",
            "interestedVehicleId": None
        }
        create_lead_resp = requests.post(
            BASE_URL + LEADS_ENDPOINT,
            headers={**headers, "Content-Type": "application/json"},
            json=lead_payload,
            timeout=30
        )
        assert create_lead_resp.status_code == 201, f"Lead creation failed: {create_lead_resp.text}"
        lead_id = create_lead_resp.json().get("id")
        assert lead_id, "No lead id returned after creation"

        # Update lead status using PATCH /api/v1/leads/{id}/status
        update_payload = {
            "newStatus": 1,
            "notes": "Called lead"
        }
        update_resp = requests.patch(
            f"{BASE_URL}/api/v1/leads/{lead_id}/status",
            headers={**headers, "Content-Type": "application/json"},
            json=update_payload,
            timeout=30
        )
        # Per spec, response 204 NoContent expected
        assert update_resp.status_code == 204, f"Failed to update lead status: {update_resp.text}"

        # Validate lead status updated correctly by fetching the lead
        get_lead_resp = requests.get(
            f"{BASE_URL}/api/v1/leads/{lead_id}",
            headers=headers,
            timeout=30
        )
        assert get_lead_resp.status_code == 200, f"Failed to get lead after status update: {get_lead_resp.text}"
        lead_data = get_lead_resp.json()
        # Correct assertion based on actual response: status is string 'Contactado'
        assert lead_data.get("status") == "Contactado", f"Lead status was not updated correctly. Got: {lead_data.get('status')}"

    finally:
        # Clean up - delete the created lead if created
        if lead_id:
            requests.delete(
                f"{BASE_URL}/api/v1/leads/{lead_id}",
                headers=headers,
                timeout=30
            )

test_update_lead_status_with_valid_data()
